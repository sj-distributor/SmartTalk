using AutoMapper;
using SmartTalk.Core.Domain.HrInterView;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.HrInterView;
using SmartTalk.Messages.Dto.HrInterView;
using SmartTalk.Messages.Requests.HrInterView;
using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using OpenAI.Chat;
using Serilog;
using Smarties.Messages.DTO.OpenAi;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Asr;
using SmartTalk.Messages.Dto.Smarties;
using SmartTalk.Messages.Dto.WebSocket;
using SmartTalk.Messages.Enums.HrInterView;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SmartTalk.Core.Services.HrInterView;

public interface IHrInterViewService : IScopedDependency
{
    Task<AddOrUpdateHrInterViewSettingResponse> AddOrUpdateHrInterViewSettingAsync(AddOrUpdateHrInterViewSettingCommand command, CancellationToken cancellationToken);
    
    Task<GetHrInterViewSettingsResponse> GetHrInterViewSettingsAsync(GetHrInterViewSettingsRequest request, CancellationToken cancellationToken);
    
    Task<GetHrInterViewSessionsResponse> GetHrInterViewSessionsAsync(GetHrInterViewSessionsRequest request, CancellationToken cancellationToken);
    
    Task ConnectWebSocketAsync(ConnectHrInterViewCommand command, CancellationToken cancellationToken);
}

public class HrInterViewService : IHrInterViewService
{
    private readonly IMapper _mapper;
    private readonly IAsrClient _asrClient;
    private readonly ISpeechClint _speechClint;
    private readonly OpenAiSettings _openAiSettings;
    private readonly ISmartiesClient _smartiesClient;
    private readonly IHrInterViewDataProvider _hrInterViewDataProvider;
    private readonly ISmartiesHttpClientFactory _httpClientFactory;
    public HrInterViewService(IMapper mapper, IAsrClient asrClient, ISpeechClint speechClint, ISmartiesClient smartiesClient, OpenAiSettings openAiSettings, IHrInterViewDataProvider hrInterViewDataProvider, ISmartiesHttpClientFactory httpClientFactory)
    {
        _mapper = mapper;
        _asrClient = asrClient;
        _speechClint = speechClint;
        _smartiesClient = smartiesClient;
        _openAiSettings = openAiSettings;
        _hrInterViewDataProvider = hrInterViewDataProvider;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AddOrUpdateHrInterViewSettingResponse> AddOrUpdateHrInterViewSettingAsync(AddOrUpdateHrInterViewSettingCommand command, CancellationToken cancellationToken)
    {
        var newSetting = _mapper.Map<HrInterViewSetting>(command.Setting);
        if (command.Setting.Id.HasValue)
        {
            var setting = await _hrInterViewDataProvider.GetHrInterViewSettingByIdAsync(command.Setting.Id.Value, cancellationToken).ConfigureAwait(false);
            
            var oldQuestions = await _hrInterViewDataProvider.GetHrInterViewSettingQuestionsBySessionIdAsync(setting.SessionId, cancellationToken).ConfigureAwait(false);
        
            if (oldQuestions.Any()) await _hrInterViewDataProvider.DeleteHrInterViewSettingQuestionsAsync(oldQuestions, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            await _hrInterViewDataProvider.UpdateHrInterViewSettingAsync(newSetting, cancellationToken:cancellationToken).ConfigureAwait(false);
        }
        else await _hrInterViewDataProvider.AddHrInterViewSettingAsync(newSetting, cancellationToken:cancellationToken).ConfigureAwait(false);
        
        command.Questions.ForEach(x => x.SettingId = newSetting.Id);
        
        await _hrInterViewDataProvider.AddHrInterViewSettingQuestionsAsync(_mapper.Map<List<HrInterViewSettingQuestion>>(command.Questions), cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return new AddOrUpdateHrInterViewSettingResponse();
    }

    public async Task<GetHrInterViewSettingsResponse> GetHrInterViewSettingsAsync(GetHrInterViewSettingsRequest request, CancellationToken cancellationToken)
    {
        var (settings, count) = await _hrInterViewDataProvider.GetHrInterViewSettingsAsync(request.SettingId, request.PageIndex, request.PageSzie, cancellationToken).ConfigureAwait(false);
        
        return new GetHrInterViewSettingsResponse
        {
            Settings = _mapper.Map<List<HrInterViewSettingDto>>(settings),
            TotalCount = count
        };
    }

    public async Task<GetHrInterViewSessionsResponse> GetHrInterViewSessionsAsync(GetHrInterViewSessionsRequest request, CancellationToken cancellationToken)
    {
        var (sessions, count) = await _hrInterViewDataProvider.GetHrInterViewSessionsAsync(request.SettingId, request.PageIndex, request.PageSzie, cancellationToken).ConfigureAwait(false);
        
        return new GetHrInterViewSessionsResponse
        {
            SessionGroups = sessions,
            TotalCount = count
        };
    }

    public async Task ConnectWebSocketAsync(ConnectHrInterViewCommand command, CancellationToken cancellationToken)
    {
        try
        {
            Log.Information("Connect to hr interview WebSocket for session {SessionId} on host {Host}", command.SessionId, command.Host);
           
            await SendWelcomeAndFirstQuestionAsync(command.WebSocket, command.SessionId, cancellationToken).ConfigureAwait(false);
            
            var buffer = new byte[1024 * 30];

            while (command.WebSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                Log.Information("Connect to hr interview WebSocket start");
                
                var result = await command.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    Log.Information("WebSocket receive message {@message}", message);

                    var messageObj = JsonConvert.DeserializeObject<HrInterViewQuestionEventDto>(message);

                    await HandleWebSocketMessageAsync(command.WebSocket, command.SessionId, messageObj, cancellationToken).ConfigureAwait(false);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    Log.Information("WebSocket close message received for session {SessionId}", command.SessionId);

                    await command.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", cancellationToken).ConfigureAwait(false);

                    break;
                }
            }
        }
        catch (WebSocketException ex)
        {
            throw new InvalidOperationException($"WebSocket connection error for session {command.SessionId}, ex: {ex.Message}", ex);
        }
    }
    
    private async Task SendWelcomeAndFirstQuestionAsync(WebSocket webSocket, Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var setting = await _hrInterViewDataProvider.GetHrInterViewSettingBySessionIdAsync(sessionId, cancellationToken).ConfigureAwait(false);
            
            if (setting == null) await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "No setting found", cancellationToken).ConfigureAwait(false);
            
            Log.Information("SendWelcomeAndFirstQuestionAsync setting:{@setting}", setting);
            
            var settingQuestions = await _hrInterViewDataProvider.GetHrInterViewSettingQuestionsBySessionIdAsync(sessionId, cancellationToken).ConfigureAwait(false);
            
            Log.Information("SendWelcomeAndFirstQuestionAsync settingQuestions:{@settingQuestions}", settingQuestions);
            
            var questions = settingQuestions.Where(x => x.Count > 0).ToList();
            
            if (!questions.Any()) await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "No questions found", cancellationToken).ConfigureAwait(false);
        
            await ConvertAndSendWebSocketMessageAsync(webSocket, sessionId, "WELCOME", setting.Welcome, setting.EndMessage, cancellationToken).ConfigureAwait(false);

            var firstQuestion = JsonConvert.DeserializeObject<List<string>>(questions.FirstOrDefault()!.Question).FirstOrDefault();

            if (firstQuestion != null)
            {
                await ConvertAndSendWebSocketMessageAsync(webSocket, sessionId, "MESSAGE", firstQuestion, cancellationToken: cancellationToken).ConfigureAwait(false);
                
                Log.Information("SendWelcomeAndFirstQuestionAsync questions:{@questions}", questions);
                
                if (questions.FirstOrDefault() is not null) questions.FirstOrDefault()!.Count -= 1;
                
                Log.Information("SendWelcomeAndFirstQuestionAsync questions after:{@questions}", questions);
                
                await _hrInterViewDataProvider.UpdateHrInterViewSettingQuestionsAsync(questions, cancellationToken:cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"SendWelcomeAndFirstQuestionAsync error message:{ex.Message}");
        }
    }
    
    private async Task HandleWebSocketMessageAsync(WebSocket webSocket, Guid sessionId, HrInterViewQuestionEventDto message, CancellationToken cancellationToken)
    {
        try
        {
            if (message.EventType == "RESPONSE_EVENT")
            {
                Log.Information("HandleWebSocketMessageAsync sessionId:{@sessionId}, message:{@message}", sessionId, message);

                var questions = await _hrInterViewDataProvider.GetHrInterViewSettingQuestionsBySessionIdAsync(sessionId, cancellationToken).ConfigureAwait(false);
                
                var fileBytes = await _httpClientFactory.GetAsync<byte[]>(message.Message, cancellationToken).ConfigureAwait(false);
                
                var answers = await _asrClient.TranscriptionAsync(new AsrTranscriptionDto { File = fileBytes }, cancellationToken).ConfigureAwait(false);
                
                await _hrInterViewDataProvider.AddHrInterViewSessionAsync(new HrInterViewSession
                {
                    SessionId = sessionId,
                    Message = answers.Text,
                    FileUrl = message.Message,
                    QuestionType = HrInterViewSessionQuestionType.User
                }, cancellationToken:cancellationToken).ConfigureAwait(false);
                
                if (questions.Any(x => x.Count <= 0)) return;
                
                var context = await GetHrInterViewSessionContextAsync(sessionId, cancellationToken).ConfigureAwait(false);
                    
                var responseNextQuestion = await MatchingReasonableNextQuestionAsync(answers.Text, questions, context, fileBytes, cancellationToken).ConfigureAwait(false);

                var fileUrl = await UploadAndRetryFileAsync(responseNextQuestion.AudioBytes.ToArray(), cancellationToken:cancellationToken).ConfigureAwait(false);

                await SendWebSocketMessageAsync(webSocket, new HrInterViewQuestionEventDto
                {
                    SessionId = sessionId,
                    EventType = "MESSAGE",
                    Message = responseNextQuestion.Transcript,
                    MessageFileUrl = fileUrl
                }, cancellationToken).ConfigureAwait(false);
                    
                await _hrInterViewDataProvider.AddHrInterViewSessionAsync(new HrInterViewSession
                {
                    SessionId = sessionId,
                    Message = responseNextQuestion.Transcript,
                    FileUrl = fileUrl,
                    QuestionType = HrInterViewSessionQuestionType.Assistant
                }, cancellationToken:cancellationToken).ConfigureAwait(false);
                    
                var question = questions.Where(x => x.Count > 0).FirstOrDefault();
                if (question != null)
                {
                    question.Count -= 1;
                    await _hrInterViewDataProvider.UpdateHrInterViewSettingQuestionsAsync(questions, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to handle WebSocket message for session {sessionId}", ex);
        }
    }
    
    private async Task<ChatOutputAudio> MatchingReasonableNextQuestionAsync(string userQuestion, List<HrInterViewSettingQuestion> candidateQuestions, string context, byte[] audioContent, CancellationToken cancellationToken)
    {
        var questionListBuilder = new StringBuilder();
        var grouped = candidateQuestions
            .GroupBy(q => q.Id)
            .OrderBy(g => g.Key);

        var globalIndex = 1;
        foreach (var group in grouped)
        {
            questionListBuilder.AppendLine();
            questionListBuilder.AppendLine($"类型 ID：{group.Key}");
            group.ForEach(x => questionListBuilder.AppendLine($"“{x.Type}”这类的问题有: {x.Question}"));
        }
        
        ChatClient client = new("gpt-4o-audio-preview", _openAiSettings.ApiKey);
        var audioData = BinaryData.FromBytes(audioContent);
        List<ChatMessage> messages =
        [
            new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(audioData, ChatInputAudioFormat.Wav)),
            new UserChatMessage($"""
                                You are a professional interviewer, currently conducting a conversation with an interviewee. Based on the interviewee's responses, please perform the following tasks:
                                1. Provide a brief, professional evaluation of the interviewee's response, including affirmation and highlighting key points (additional points for improvement should be brief and should not be repeated). Ensure your overall response is natural, coherent, and comprehensive.
                                2. In a natural transition, select an appropriate question from the "Question List" below.
                                3. **Regardless of the user's language, your final output message must be in English.**
                                4. Ask only one question at a time (and do not repeat questions that have already been asked).
                                * The question list is as follows: {questionListBuilder}
                                5. Response Style Requirements:
                                * Use natural, colloquial language, not overly official. Maintain professionalism but avoid being mechanical.
                                * Avoid repeating what the interviewee has just said.
                                * Use natural transitions, including but not limited to using phrases like "I see. I'd also like to know..." and "That sounds great. My next question is..." Ensure the overall tone is consistent and the transitions are natural. Always use English. * Do not re-ask or re-describe questions that have already been asked.
                                * Once all questions have been asked, you can generate an appropriate closing statement.
                                * **Regardless of the user's language, your final output message must be in English.**
                                Before you output, take a deep breath and consider whether your response meets my formatting requirements.
                                The following context helps you filter questions that have already been asked: {context}
                                The current user's response is: {userQuestion}
                                """)
        ];

        ChatCompletionOptions options = new()
        {
            ResponseModalities = ChatResponseModalities.Text | ChatResponseModalities.Audio,
            AudioOptions = new ChatAudioOptions(new ChatOutputAudioVoice("cedar"), ChatOutputAudioFormat.Wav)
        };

        ChatCompletion completion = await client.CompleteChatAsync(messages, options, cancellationToken);
        
        Log.Information("MatchingReasonableNextQuestionAsync next question response:{@completion} ", completion);

        return completion.OutputAudio;
    }

    private async Task ConvertAndSendWebSocketMessageAsync(WebSocket webSocket, Guid sessionId, string eventType, string message, string endMessage = null, CancellationToken cancellationToken = default)
    {
        var messageAudio = await ConvertTextToSpeechAsync(message, cancellationToken).ConfigureAwait(false);
        
        var endMessageAudio = "";
       
        if (endMessage != null && !string.IsNullOrEmpty(endMessage)) endMessageAudio = await ConvertTextToSpeechAsync(endMessage, cancellationToken).ConfigureAwait(false);
            
        var welcomeEvent = new HrInterViewQuestionEventDto
        {
            SessionId = sessionId,
            EventType = eventType,
            Message = message, 
            MessageFileUrl = messageAudio,
            EndMessage = string.IsNullOrEmpty(endMessage) ? "" : endMessage,
            EndMessageFileUrl = string.IsNullOrEmpty(endMessageAudio) ? "" : endMessageAudio
        };

        await SendWebSocketMessageAsync(webSocket, welcomeEvent, cancellationToken).ConfigureAwait(false);
        
        await _hrInterViewDataProvider.AddHrInterViewSessionAsync(new HrInterViewSession
        {
            SessionId = sessionId,
            Message = message,
            FileUrl = messageAudio,
            QuestionType = HrInterViewSessionQuestionType.Assistant }, cancellationToken:cancellationToken).ConfigureAwait(false);
        
        if (endMessage != null && !string.IsNullOrEmpty(endMessage))  
            await _hrInterViewDataProvider.AddHrInterViewSessionAsync(new HrInterViewSession
            {
                SessionId = sessionId,
                Message = endMessage,
                FileUrl = endMessageAudio,
                QuestionType = HrInterViewSessionQuestionType.Assistant,
                CreatedDate =  new DateTimeOffset(new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc))
            }, cancellationToken:cancellationToken).ConfigureAwait(false);
    }
    
    private async Task SendWebSocketMessageAsync(WebSocket webSocket, HrInterViewQuestionEventDto message, CancellationToken cancellationToken)
    {
        try
        {
            if (webSocket.State == WebSocketState.Open)
            {
                var json = JsonConvert.SerializeObject(message);
                var bytes = Encoding.UTF8.GetBytes(json);
                
                await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to send WebSocket message", ex);
        }
    }
    
    private async Task<string> GetHrInterViewSessionContextAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var (sessions, _) = await _hrInterViewDataProvider.GetHrInterViewSessionsAsync(sessionId: sessionId, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return string.Join("\n", sessions.FirstOrDefault()!.Sessions.Where(x => x.CreatedDate !=  new DateTimeOffset(new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc))).Select(x => x.QuestionType == HrInterViewSessionQuestionType.Assistant? $"问：{x.Message}" : $"答：{x.Message}" ).ToList());
    }
    
    private async Task<string> ConvertTextToSpeechAsync(string text, CancellationToken cancellationToken)
    {
        async Task<string?> GetAudioTextAsync()
        {
            var response = await _speechClint.GetAudioFromTextAsync(
                new TextToSpeechDto
                {
                    Text = text,
                    VoiceId = 203
                }, cancellationToken).ConfigureAwait(false);

            Log.Information("ConvertTextToSpeechAsync response: {@Response}", response);
            return response?.Result;
        }

        var result = await GetAudioTextAsync().ConfigureAwait(false);
        if (result != null)
            return result;
        
        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        return await GetAudioTextAsync().ConfigureAwait(false);
    }
    
    private async Task<string> UploadAndRetryFileAsync(byte[] fileBytes, int attempt = 0, CancellationToken cancellationToken = default)
    {
        var response = await _smartiesClient.UploadFileAsync(fileBytes, cancellationToken).ConfigureAwait(false);

        if (response.Data is null || string.IsNullOrEmpty(response.Data.FileUrl))
        {
            attempt++;

            if (attempt > 3) throw new Exception($"Failed to upload file after {attempt} attempts");
            
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            
            return await UploadAndRetryFileAsync(fileBytes, attempt, cancellationToken).ConfigureAwait(false);
        }
        
        return response.Data.FileUrl;
    }
}