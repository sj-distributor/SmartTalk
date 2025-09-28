using AutoMapper;
using SmartTalk.Core.Domain.HrInterView;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.HrInterView;
using SmartTalk.Messages.Dto.HrInterView;
using SmartTalk.Messages.Requests.HrInterView;
using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using Serilog;
using Smarties.Messages.DTO.OpenAi;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Messages.Dto.Asr;
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
    private readonly ISmartiesClient _smartiesClient;
    private readonly IHrInterViewDataProvider _hrInterViewDataProvider;

    public HrInterViewService(IMapper mapper, IAsrClient asrClient, ISpeechClint speechClint, ISmartiesClient smartiesClient, IHrInterViewDataProvider hrInterViewDataProvider)
    {
        _mapper = mapper;
        _asrClient = asrClient;
        _speechClint = speechClint;
        _smartiesClient = smartiesClient;
        _hrInterViewDataProvider = hrInterViewDataProvider;
    }

    public async Task<AddOrUpdateHrInterViewSettingResponse> AddOrUpdateHrInterViewSettingAsync(AddOrUpdateHrInterViewSettingCommand command, CancellationToken cancellationToken)
    {
        var setting = _mapper.Map<HrInterViewSetting>(command.Setting);
        
        var exists = await _hrInterViewDataProvider.GetHrInterViewSettingByIdAsync(command.Setting.Id, cancellationToken).ConfigureAwait(false);

        if (exists == null) await _hrInterViewDataProvider.AddHrInterViewSettingAsync(setting, cancellationToken:cancellationToken).ConfigureAwait(false);
        else await _hrInterViewDataProvider.UpdateHrInterViewSettingAsync(setting, cancellationToken:cancellationToken).ConfigureAwait(false);

        var existsQuestion = await _hrInterViewDataProvider.GetHrInterViewSettingQuestionsByIdAsync(command.Questions.Select(x => x.Id).ToList(), cancellationToken).ConfigureAwait(false);
        
        if (existsQuestion.Any()) await _hrInterViewDataProvider.DeleteHrInterViewSettingQuestionsAsync(existsQuestion, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var firstQuestion = command.Questions.FirstOrDefault();
        if (firstQuestion != null && firstQuestion.Question.Length > 0)
        {
            firstQuestion.Count -= firstQuestion.Count;
        }
        
        command.Questions.ForEach(x => x.SettingId = setting.Id);
        
        await _hrInterViewDataProvider.AddHrInterViewSettingQuestionsAsync(_mapper.Map<List<HrInterViewSettingQuestion>>(command.Questions), cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await StartWebSocketCommunicationAsync(command, cancellationToken).ConfigureAwait(false);
        
        return new AddOrUpdateHrInterViewSettingResponse();
    }
     
    private async Task StartWebSocketCommunicationAsync(AddOrUpdateHrInterViewSettingCommand command, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new ClientWebSocket();
            await client.ConnectAsync(new Uri($"wss://{command.Host}/api/HrInterViewC/connect/{command.Setting.SessionId}"), cancellationToken).ConfigureAwait(false);
           
            if (client.State != WebSocketState.Open) return;

            await ConvertAndSendWebSocketMessageAsync(client, command.Setting.SessionId, "WELCOME", command.Setting.Welcome, command.Setting.EndMessage, cancellationToken).ConfigureAwait(false);
            
            if (command.Questions?.Any() == true)
            {
                var questions = command.Questions.FirstOrDefault()!.Question;

                var firstQuestion = JsonConvert.DeserializeObject<List<string>>(questions).FirstOrDefault();
                
                await ConvertAndSendWebSocketMessageAsync(client, command.Setting.SessionId, "MESSAGE", firstQuestion, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to start WebSocket communication for session {command.Setting.SessionId}", ex);
        }
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
        var (settings, count) = await _hrInterViewDataProvider.GetHrInterViewSessionsAsync(request.SettingId, request.PageIndex, request.PageSzie, cancellationToken).ConfigureAwait(false);
        
        return new GetHrInterViewSessionsResponse
        {
            Sessions = _mapper.Map<List<HrInterViewSessionDto>>(settings),
            TotalCount = count
        };
    }

    public async Task AddHrInterViewSessionsAsync(List<HrInterViewSessionDto> sessions, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task ConnectWebSocketAsync(ConnectHrInterViewCommand command, CancellationToken cancellationToken)
    {
        try
        {
            Log.Information("Connect to hr interview WebSocket");
            
            var buffer = new byte[1024 * 30];
            
            while (command.WebSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await command.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    await HandleWebSocketMessageAsync(command.WebSocket, command.SessionId, JsonConvert.DeserializeObject<HrInterViewQuestionEventResponseDto>(message), cancellationToken).ConfigureAwait(false);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await command.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", cancellationToken).ConfigureAwait(false);
                    break;
                }
            }
        }
        catch (WebSocketException ex)
        {
            throw new InvalidOperationException($"WebSocket connection error for session {command.SessionId}", ex);
        }
    }
    
    private async Task HandleWebSocketMessageAsync(WebSocket webSocket, Guid sessionId, HrInterViewQuestionEventResponseDto message, CancellationToken cancellationToken)
    {
        try
        {
            if (message.EventType == "RESPONSE_EVENT")
            {
                var answersFile = await _smartiesClient.UploadFileAsync(message.Message, cancellationToken).ConfigureAwait(false);
                var answers = await _asrClient.TranscriptionAsync(new AsrTranscriptionDto { File = message.Message }, cancellationToken).ConfigureAwait(false);
                
                await _hrInterViewDataProvider.AddHrInterViewSessionAsync(new HrInterViewSession
                {
                    SessionId = sessionId,
                    Message = answers.Text,
                    FileUrl = answersFile.Data.FileUrl,
                    QuestionType = HrInterViewSessionQuestionType.User
                }, cancellationToken:cancellationToken).ConfigureAwait(false);
                
                var questions = (await _hrInterViewDataProvider.GetHrInterViewSettingQuestionsBySessionIdAsync(sessionId, cancellationToken).ConfigureAwait(false)).Where(x => x.Count > 0).ToList();

                var context = await GetHrInterViewSessionContextAsync(sessionId, cancellationToken).ConfigureAwait(false);
                
                var matchQuestion = await FindMostSimilarQuestionUsingLLMAsync(answers.Text, questions, context, cancellationToken).ConfigureAwait(false);
                
                var matchQuestionAudio = await ConvertTextToSpeechAsync(matchQuestion.Question, cancellationToken).ConfigureAwait(false);
                
                await SendWebSocketMessageAsync(webSocket, new HrInterViewQuestionEventDto
                {
                    SessionId = sessionId,
                    EventType = "MESSAGE",
                    Message = matchQuestionAudio,
                }, cancellationToken).ConfigureAwait(false);
                
                await _hrInterViewDataProvider.AddHrInterViewSessionAsync(new HrInterViewSession
                {
                    SessionId = sessionId,
                    Message = matchQuestion.Question,
                    FileUrl = matchQuestionAudio,
                    QuestionType = HrInterViewSessionQuestionType.Assistant
                }, cancellationToken:cancellationToken).ConfigureAwait(false);
                
                var updateQuestions = await _hrInterViewDataProvider.GetHrInterViewSettingQuestionsByIdAsync(new List<int> {matchQuestion.SettingQuestionId}, cancellationToken).ConfigureAwait(false);
             
                updateQuestions.ForEach(x => x.Count -= x.Count);
                
                await _hrInterViewDataProvider.UpdateHrInterViewSettingQuestionsAsync(updateQuestions, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to handle WebSocket message for session {sessionId}", ex);
        }
    }

    private async Task ConvertAndSendWebSocketMessageAsync(ClientWebSocket webSocket, Guid sessionId, string eventType, string message, string endMessage = null, CancellationToken cancellationToken = default)
    {
        var messageAudio = await ConvertTextToSpeechAsync(message, cancellationToken).ConfigureAwait(false);
       
        var endMessageAudio = "";
       
        if (endMessage != null && !string.IsNullOrEmpty(endMessage)) endMessageAudio = await ConvertTextToSpeechAsync(endMessage, cancellationToken).ConfigureAwait(false);
            
        var welcomeEvent = new HrInterViewQuestionEventDto
        {
            SessionId = sessionId,
            EventType = eventType,
            Message = message, 
            EndMessage = string.IsNullOrEmpty(endMessage) ? "" : endMessage, 
        };

        await SendWebSocketMessageAsync(webSocket, welcomeEvent, cancellationToken).ConfigureAwait(false);
       
        await _hrInterViewDataProvider.AddHrInterViewSessionAsync(new HrInterViewSession
        {
            SessionId = sessionId,
            Message = message,
            FileUrl = messageAudio,
            QuestionType = HrInterViewSessionQuestionType.Assistant
        }, cancellationToken:cancellationToken).ConfigureAwait(false);
        
        if (endMessage != null && !string.IsNullOrEmpty(endMessage))  
            await _hrInterViewDataProvider.AddHrInterViewSessionAsync(new HrInterViewSession
            {
                SessionId = sessionId,
                Message = endMessage,
                FileUrl = endMessageAudio,
                QuestionType = HrInterViewSessionQuestionType.Assistant,
                CreatedDate = DateTimeOffset.MaxValue
            }, cancellationToken:cancellationToken).ConfigureAwait(false);
    }
    
    private async Task SendWebSocketMessageAsync(WebSocket webSocket, HrInterViewQuestionEventDto message, CancellationToken cancellationToken)
    {
        try
        {
            if (webSocket.State == WebSocketState.Open)
            {
                var json = JsonSerializer.Serialize(message);
                var bytes = Encoding.UTF8.GetBytes(json);
                
                await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to send WebSocket message", ex);
        }
    }
    
    public async Task<MatchedQuestionResultDto> FindMostSimilarQuestionUsingLLMAsync(string userQuestion, List<HrInterViewSettingQuestion> candidateQuestions, string context, CancellationToken cancellationToken)
    {
        var promptPrefix = """
                           你是一位专业的面试官，正在与面试者进行对话。请根据面试者的回答执行以下任务：
                           1. 对面试者的回答做出简短、专业的评价，可以肯定、指出亮点，或礼貌地指出可以补充/改进的地方；
                           2. 在自然的对话过渡中，从下方“问题列表”中选择一个合适的问题继续提问；
                           3. 每次只提一个问题；
                           4. 请以如下格式输出：
                           * chosen_question: 所选问题的内容；
                           * question_type: 问题的类型名称（如：性格类、专业类等）；
                           * question_type_id: 问题类型的唯一 ID（如：character、skill 等）；
                           * follow_up_message: 面试官的完整回复内容（包含对上一问题的评价 + 自然过渡 + 当前问题）。

                           问题列表如下：
                           """;
        
        var questionListBuilder = new StringBuilder();
        var grouped = candidateQuestions
            .GroupBy(q => q.Type)
            .OrderBy(g => g.Key);

        var globalIndex = 1;
        foreach (var group in grouped)
        {
            questionListBuilder.AppendLine();
            questionListBuilder.AppendLine($"类型：{group.Key} | 类型 ID：{group.Key}");

            foreach (var question in group)
            {
                questionListBuilder.AppendLine($"{globalIndex++}. {question.Questions}");
            }
        }

        var styleRequirements = $"""
                                 回复风格要求：
                                 * 用语自然、口语化、不过度官方；
                                 * 保持专业，但避免过于机械；
                                 * 不要重复面试者刚才说过的内容；
                                 * 若面试者回答不清晰，可委婉追问细节或举例说明；
                                 * 过渡要自然，例如使用 “了解了，那我也想了解一下…”、“听起来很不错，那接下来我想问的是…” 等句式。
                                 * 对于提问过的问题不进行二次提问
                                 以下是上下文帮助你过滤已经问过的问题：{context}
                                 """;
        
        var fullPrompt = promptPrefix + questionListBuilder + "\n" + styleRequirements;

        var request = new AskGptRequest
        {
            Model = OpenAiModel.Gpt4o,
            Messages = new List<CompletionsRequestMessageDto>
            {
                new CompletionsRequestMessageDto
                {
                    Role = "system",
                    Content = new CompletionsStringContent(fullPrompt)
                },
                new CompletionsRequestMessageDto
                {
                    Role = "user",
                    Content = new CompletionsStringContent(userQuestion)
                }
            },
            Temperature = 0.1,
            ResponseFormat = new CompletionResponseFormatDto
            {
                Type = "json_object" 
            }
        };

        var response = await _smartiesClient.PerformQueryAsync(request, cancellationToken).ConfigureAwait(false);
        
        return JsonConvert.DeserializeObject<MatchedQuestionResultDto>(response.Data.Response);
    }

    private async Task<string> GetHrInterViewSessionContextAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var (sessions, _) = await _hrInterViewDataProvider.GetHrInterViewSessionsAsync(sessionId: sessionId, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return string.Join(" ", sessions.Where(x => x.CreatedDate != DateTimeOffset.MaxValue).Select(x => x.QuestionType == HrInterViewSessionQuestionType.Assistant? $"问：{x.Message}\n" : $"答：{x.Message}\n" ).ToList());
    }
    
    private async Task<string> ConvertTextToSpeechAsync(string text, CancellationToken cancellationToken)
    {
        var fileResponse = await _speechClint.GetAudioFromTextAsync(new TextToSpeechDto
        {
            Text = text,
            VoiceId = 415
        }, cancellationToken).ConfigureAwait(false);
        
        return fileResponse.Result;
    }
}