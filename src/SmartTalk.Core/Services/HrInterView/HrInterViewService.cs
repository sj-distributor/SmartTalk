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
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Dto.Asr;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Dto.Smarties;
using SmartTalk.Messages.Dto.WebSocket;
using SmartTalk.Messages.Enums.HrInterView;
using SmartTalk.Messages.Events.HrInterView;

namespace SmartTalk.Core.Services.HrInterView;

public interface IHrInterViewService : IScopedDependency
{
    Task<AddOrUpdateHrInterViewSettingResponse> AddOrUpdateHrInterViewSettingAsync(AddOrUpdateHrInterViewSettingCommand command, CancellationToken cancellationToken);
    
    Task<GetHrInterViewSettingsResponse> GetHrInterViewSettingsAsync(GetHrInterViewSettingsRequest request, CancellationToken cancellationToken);
    
    Task<GetHrInterViewSessionsResponse> GetHrInterViewSessionsAsync(GetHrInterViewSessionsRequest request, CancellationToken cancellationToken);
    
    Task<ConnectWebSocketEvent> ConnectWebSocketAsync(ConnectHrInterViewCommand command, CancellationToken cancellationToken);
}

public class HrInterViewService : IHrInterViewService
{
    private readonly IMapper _mapper;
    private readonly IAsrClient _asrClient;
    private readonly ISpeechClint _speechClint;
    private readonly OpenAiSettings _openAiSettings;
    private readonly ISmartiesClient _smartiesClient;
    private readonly IAttachmentUtilService _attachmentUtilService;
    private readonly IHrInterViewDataProvider _hrInterViewDataProvider;
    private readonly ISmartiesHttpClientFactory _httpClientFactory;
    public HrInterViewService(IMapper mapper, IAsrClient asrClient, ISpeechClint speechClint, ISmartiesClient smartiesClient, IAttachmentUtilService attachmentUtilService, OpenAiSettings openAiSettings, IHrInterViewDataProvider hrInterViewDataProvider, ISmartiesHttpClientFactory httpClientFactory)
    {
        _mapper = mapper;
        _asrClient = asrClient;
        _speechClint = speechClint;
        _smartiesClient = smartiesClient;
        _attachmentUtilService = attachmentUtilService;
        _openAiSettings = openAiSettings;
        _hrInterViewDataProvider = hrInterViewDataProvider;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AddOrUpdateHrInterViewSettingResponse> AddOrUpdateHrInterViewSettingAsync(AddOrUpdateHrInterViewSettingCommand command, CancellationToken cancellationToken)
    {
        var newSetting = _mapper.Map<HrInterViewSetting>(command.Setting);

        var changeAudioList = new Dictionary<int, string>();
        if (command.ChangeQusetionIds != null && command.ChangeQusetionIds.Any())
        {
            foreach (var change in command.ChangeQusetionIds) 
                changeAudioList.Add(change.Id, await ConvertTextToSpeechAsync(change.Questions, cancellationToken).ConfigureAwait(false));
        }
        
        newSetting.Welcome = UpdateSpeechIfChanged(newSetting.Welcome, changeAudioList);
        newSetting.EndMessage = UpdateSpeechIfChanged(newSetting.EndMessage, changeAudioList);
        
        if (command.Setting.Id.HasValue)
        {
            var setting = await _hrInterViewDataProvider.GetHrInterViewSettingByIdAsync(command.Setting.Id.Value, cancellationToken).ConfigureAwait(false);
            
            var oldQuestions = await _hrInterViewDataProvider.GetHrInterViewSettingQuestionsBySessionIdAsync(setting.SessionId, cancellationToken).ConfigureAwait(false);
        
            if (oldQuestions.Any()) await _hrInterViewDataProvider.DeleteHrInterViewSettingQuestionsAsync(oldQuestions, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            await _hrInterViewDataProvider.UpdateHrInterViewSettingAsync(newSetting, cancellationToken:cancellationToken).ConfigureAwait(false);
        }
        else await _hrInterViewDataProvider.AddHrInterViewSettingAsync(newSetting, cancellationToken:cancellationToken).ConfigureAwait(false);

        var insertAudioList = new Dictionary<int, List<HrInterViewQuestionsDto>>();
        var maxQuestionId = 0;
        
        foreach (var questionList in command.Questions)
        {
            questionList.SettingId = newSetting.Id;
            questionList.OriginCount = questionList.Count;
            var type = JsonConvert.DeserializeObject<HrInterViewQuestionsDto>(questionList.Type);

            if (type.QuestionId.HasValue && changeAudioList.TryGetValue(type.QuestionId.Value, out var audioTypeUrl))
            {
                type.Url = audioTypeUrl;
                questionList.Type = JsonConvert.SerializeObject(type);
            }

            var questions = JsonConvert.DeserializeObject<List<HrInterViewQuestionsDto>>(questionList.Question);
            
            if (questions == null || !questions.Any()) continue;
            
            maxQuestionId = Math.Max(maxQuestionId, questions.Where(x => x.QuestionId.HasValue).Max(x => x.QuestionId.Value));
            var toInsert = questions.Where(x => !x.QuestionId.HasValue).ToList();
            if (toInsert.Any())
            {
                if (!insertAudioList.ContainsKey(questionList.Id))
                    insertAudioList[questionList.Id] = new List<HrInterViewQuestionsDto>();
                insertAudioList[questionList.Id].AddRange(toInsert);
            }
            
            questions.ForEach(question =>
            {
                if (question.QuestionId.HasValue && changeAudioList.TryGetValue(question.QuestionId.Value, out var audioQuestionUrl)) question.Url = audioQuestionUrl;
            });
            questionList.Question = JsonConvert.SerializeObject(questions);
        }
        
        foreach (var (key, values) in insertAudioList)
        {
            var questionSetting = command.Questions.First(x => x.Id == key);
            var questions = JsonConvert.DeserializeObject<List<HrInterViewQuestionsDto>>(questionSetting.Question) ?? new();

            foreach (var value in values)
            {
                value.Url = await ConvertTextToSpeechAsync(value.Question, cancellationToken).ConfigureAwait(false);
                value.QuestionId = ++maxQuestionId;
                questions.Add(value);
            }

            questionSetting.Question = JsonConvert.SerializeObject(questions);
        }
        
        await _hrInterViewDataProvider.AddHrInterViewSettingQuestionsAsync(_mapper.Map<List<HrInterViewSettingQuestion>>(command.Questions), cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return new AddOrUpdateHrInterViewSettingResponse();
    }
    
    private static string UpdateSpeechIfChanged(string jsonField, Dictionary<int, string> changeAudioList)
    {
        if (string.IsNullOrWhiteSpace(jsonField)) return jsonField;

        var dto = JsonConvert.DeserializeObject<HrInterViewQuestionsDto>(jsonField);
        if (dto?.QuestionId is null) return jsonField;

        if (changeAudioList.TryGetValue(dto.QuestionId.Value, out var url))
        {
            dto.Url = url;
            return JsonConvert.SerializeObject(dto);
        }

        return jsonField;
    }
    
    public async Task<GetHrInterViewSettingsResponse> GetHrInterViewSettingsAsync(GetHrInterViewSettingsRequest request, CancellationToken cancellationToken)
    {
        var (settings, count) = await _hrInterViewDataProvider.GetHrInterViewSettingsAsync(request.SettingId, request.PageIndex, request.PageSize, cancellationToken).ConfigureAwait(false);
        
        return new GetHrInterViewSettingsResponse
        {
            Settings = settings,
            TotalCount = count
        };
    }

    public async Task<GetHrInterViewSessionsResponse> GetHrInterViewSessionsAsync(GetHrInterViewSessionsRequest request, CancellationToken cancellationToken)
    {
        var (sessions, count) = await _hrInterViewDataProvider.GetHrInterViewSessionsAsync(request.SettingId, request.PageIndex, request.PageSize, cancellationToken).ConfigureAwait(false);
        
        return new GetHrInterViewSessionsResponse
        {
            SessionGroups = sessions,
            TotalCount = count
        };
    }

    public async Task<ConnectWebSocketEvent> ConnectWebSocketAsync(ConnectHrInterViewCommand command, CancellationToken cancellationToken)
    {
        try
        {
            Log.Information("Connect to hr interview WebSocket for session {@SessionId} on host {@Host}", command.SessionId, command.Host);
           
            await SendWelcomeAndFirstQuestionAsync(command.WebSocket, command.SessionId, cancellationToken).ConfigureAwait(false);
            
            var buffer = new byte[1024 * 30];

            var fileAudio = new List<string>();
            
            while (command.WebSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await command.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                Log.Information("Connect to hr interview WebSocket start");
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    Log.Information("WebSocket receive message {@message}", message);

                    var messageObj = JsonConvert.DeserializeObject<HrInterViewQuestionEventDto>(message);

                    Log.Information("WebSocket receive messageObj {@messageObj}", messageObj);
                    
                    if (messageObj.EventType == "START_EVENT" && string.IsNullOrEmpty(messageObj.Message)) fileAudio.Add(messageObj.Message);

                    if (messageObj.EventType != "RESPONSE_EVENT") continue;
                    
                    Log.Information("WebSocket receive fileAudio {@fileAudio}", fileAudio);
                    
                    var fileBytes = GetFileBytes(fileAudio);
                    await HandleWebSocketMessageAsync(command.WebSocket, command.SessionId, fileBytes, cancellationToken).ConfigureAwait(false);
                    fileAudio.Clear();
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    Log.Information("WebSocket close message received for session {@SessionId}", command.SessionId);

                    await command.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", cancellationToken).ConfigureAwait(false);

                    break;
                }
            }
            
            return new ConnectWebSocketEvent
            {
                SessionId = command.SessionId
            };
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
        
            await ConvertAndSendWebSocketMessageAsync(webSocket, sessionId, "WELCOME",  setting.Welcome, setting.EndMessage, cancellationToken:cancellationToken).ConfigureAwait(false);

            Log.Information("SendWelcomeAndFirstQuestionAsync questions:{@questions}", questions.MinBy(x => x.Id)!.Question);
            
            var firstQuestion = JsonConvert.DeserializeObject<List<HrInterViewQuestionsDto>>(questions.MinBy(x => x.Id)!.Question).FirstOrDefault();

            if (firstQuestion != null)
            {
                var firstQuestionPart = JsonConvert.DeserializeObject<HrInterViewQuestionsDto>(questions.MinBy(x => x.Id).Type);
                
                Log.Information("SendWelcomeAndFirstQuestionAsync firstQuestionPart:{@firstQuestionPart}, firstQuestion:{@firstQuestion}", firstQuestionPart, firstQuestion);
                
                await ConvertAndSendWebSocketMessageAsync(webSocket, sessionId, "MESSAGE", JsonConvert.SerializeObject(firstQuestion), firstQuestionPart:firstQuestionPart.Question, firstQuestionPartUrl: firstQuestionPart.Url, cancellationToken: cancellationToken).ConfigureAwait(false);
                
                Log.Information("SendWelcomeAndFirstQuestionAsync questions:{@questions}", questions);
                
                if (questions.FirstOrDefault() is not null) questions.MinBy(x => x.Id)!.Count -= 1;
                
                Log.Information("SendWelcomeAndFirstQuestionAsync questions after:{@questions}", questions);
                
                await _hrInterViewDataProvider.UpdateHrInterViewSettingQuestionsAsync(questions, cancellationToken:cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"SendWelcomeAndFirstQuestionAsync error message:{ex.Message}");
        }
    }
    
    private async Task HandleWebSocketMessageAsync(WebSocket webSocket, Guid sessionId, byte[] message, CancellationToken cancellationToken)
    {
        try
        {
            Log.Information("HandleWebSocketMessageAsync sessionId:{@sessionId}, message:{@message}", sessionId, message);

            var questions = await _hrInterViewDataProvider.GetHrInterViewSettingQuestionsBySessionIdAsync(sessionId, cancellationToken).ConfigureAwait(false);
            
            var remainQuestions = questions.Where(x => x.Count > 0).ToList();

            if (!remainQuestions.Any())
            {
                var lastQuestionFileUrl = await UploadFileAsync(message, sessionId, cancellationToken).ConfigureAwait(false);
            
                await _hrInterViewDataProvider.AddHrInterViewSessionAsync(new HrInterViewSession
                {
                    SessionId = sessionId,
                    Message = " ",
                    FileUrl = JsonConvert.SerializeObject(new List<string>(){lastQuestionFileUrl}),
                    QuestionType = HrInterViewSessionQuestionType.User
                }, cancellationToken: cancellationToken).ConfigureAwait(false);
                return;
            }
            
            var questionPart = remainQuestions.MinBy(x => x.Id);
            
            var (nextQuestionDto, questionList) = GetAndRemoveRandomQuestion(JsonConvert.DeserializeObject<List<HrInterViewQuestionsDto>>(questionPart.Question));
            
            var nextQuestion = nextQuestionDto.Question;
            
            var messageAudio = JsonConvert.SerializeObject(new List<string>(){nextQuestionDto.Url});
            
            if (questionPart.OriginCount == questionPart.Count)
            {
                var partType = JsonConvert.DeserializeObject<HrInterViewQuestionsDto>(questionPart.Type);
                nextQuestion = partType.Question + "\n" + nextQuestionDto.Question;
                messageAudio = JsonConvert.SerializeObject(new List<string>(){partType.Url, nextQuestionDto.Url});
            }
            
            await SendWebSocketMessageAsync(webSocket, new HrInterViewQuestionEventDto
            {
                SessionId = sessionId,
                EventType = "MESSAGE",
                Message = nextQuestion,
                MessageFileUrl = messageAudio
            }, cancellationToken).ConfigureAwait(false);
            
            var fileUrl = await UploadFileAsync(message, sessionId, cancellationToken).ConfigureAwait(false);
            
            await _hrInterViewDataProvider.AddHrInterViewSessionAsync(new HrInterViewSession
            {
                SessionId = sessionId,
                Message = " ",
                FileUrl = JsonConvert.SerializeObject(new List<string>(){fileUrl}),
                QuestionType = HrInterViewSessionQuestionType.User
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            await _hrInterViewDataProvider.AddHrInterViewSessionAsync(new HrInterViewSession
            {
                SessionId = sessionId,
                Message = nextQuestion,
                FileUrl = messageAudio,
                QuestionType = HrInterViewSessionQuestionType.Assistant
            }, cancellationToken:cancellationToken).ConfigureAwait(false);
            
            questionPart.Count -= 1;
            questionPart.Question = JsonConvert.SerializeObject(questionList);
            
            await _hrInterViewDataProvider.UpdateHrInterViewSettingQuestionsAsync(questions, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to handle WebSocket message for session {sessionId}", ex);
        }
    }
    
    public (HrInterViewQuestionsDto, List<HrInterViewQuestionsDto>) GetAndRemoveRandomQuestion(List<HrInterViewQuestionsDto> remainQuestions)
    {
        if (remainQuestions == null || remainQuestions.Count == 0)
            return (null, remainQuestions);

        var random = new Random();
        int index = random.Next(remainQuestions.Count);

        var selectedQuestion = remainQuestions[index];

        var remaining = remainQuestions.ToList();
        remaining.RemoveAt(index);

        return (selectedQuestion, remaining);
    }

    private byte[] GetFileBytes(List<string> audios)
    {
        var totalLength = 0; 
        var pcmList = new List<byte[]>();
        
        Log.Information("WebSocket receive audios {@audios}", audios);
        
        foreach (var base64 in audios)
        {
            var pcm = Convert.FromBase64String(base64);
            pcmList.Add(pcm);
            totalLength += pcm.Length;
        }
        
        var mergedPcm = new byte[totalLength];
        var offset = 0;
        foreach (var pcm in pcmList)
        {
            Buffer.BlockCopy(pcm, 0, mergedPcm, offset, pcm.Length);
            offset += pcm.Length;
        }
        
        var wavHeader = CreateWavHeader(mergedPcm.Length, 16000, 1, 16);
        return new byte[wavHeader.Length + mergedPcm.Length];
    }
    
    private static byte[] CreateWavHeader(int dataLength, int sampleRate, short channels, short bitsPerSample)
    {
        var blockAlign = (channels * bitsPerSample) / 8;
        var byteRate = sampleRate * blockAlign;
        var header = new byte[44];

        Array.Copy(Encoding.ASCII.GetBytes("RIFF"), 0, header, 0, 4);
        Array.Copy(BitConverter.GetBytes(dataLength + 36), 0, header, 4, 4);
        Array.Copy(Encoding.ASCII.GetBytes("WAVE"), 0, header, 8, 4);
        Array.Copy(Encoding.ASCII.GetBytes("fmt "), 0, header, 12, 4);
        Array.Copy(BitConverter.GetBytes(16), 0, header, 16, 4);
        Array.Copy(BitConverter.GetBytes((short)1), 0, header, 20, 2);
        Array.Copy(BitConverter.GetBytes(channels), 0, header, 22, 2);
        Array.Copy(BitConverter.GetBytes(sampleRate), 0, header, 24, 4);
        Array.Copy(BitConverter.GetBytes(byteRate), 0, header, 28, 4);
        Array.Copy(BitConverter.GetBytes(blockAlign), 0, header, 32, 2);
        Array.Copy(BitConverter.GetBytes(bitsPerSample), 0, header, 34, 2);
        Array.Copy(Encoding.ASCII.GetBytes("data"), 0, header, 36, 4);
        Array.Copy(BitConverter.GetBytes(dataLength), 0, header, 40, 4);

        return header;
    }
    
    private async Task<ChatOutputAudio> MatchingReasonableNextQuestionAsync(string userQuestion, HrInterViewSettingQuestion candidateQuestions, int currentStage, string context, byte[] audioContent, CancellationToken cancellationToken)
    {
        var questionListBuilder = new StringBuilder();
        
        questionListBuilder.AppendLine();
        questionListBuilder.AppendLine($"Question Type ID：{candidateQuestions.Id}");
        questionListBuilder.AppendLine($"“{candidateQuestions.Type}”The specific types of problems include: {candidateQuestions.Question}, The maximum number of such specific problems is:{candidateQuestions.Count}");
        
        var jsonString = """{"Id": "TypeId of the selected question type", "text": "English translation of the speech question"}""";
        
        List<ChatMessage> messages =
        [
            new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(BinaryData.FromBytes(audioContent), ChatInputAudioFormat.Wav)),
            new UserChatMessage($"""
                                You are a professional interviewer currently conducting a conversation with a respondent. Based on the respondent's response, please perform the following tasks:
                                1.You must respond with a full sentence that offers support, professional and affirmation. Use different phrasing each time to express this and keep it short and simple.
                                2.Based on the user's current response and the list of questions, select the most appropriate question from the "Question List," maintaining a natural transition.
                                3. Stage Awareness Requirement: When entering a new interview stage (e.g., Stage 1, Stage 2, etc., where the current stage is {currentStage}), determine whether this is the first question of that stage based on the context and previous questions asked.
                                  If so, include a brief introductory sentence clearly stating which stage it is, such as "We now enter Stage {currentStage}, focusing on {candidateQuestions.Type}."; otherwise, do not repeat this introduction for subsequent questions in the same stage.
                                4.Your final output must be in English, regardless of the user's language.
                                5.Ask only one question at a time (do not repeat questions you have already asked).
                                6.Strictly enforce the limit on the number of questions of each type. You must track the number of questions of that type you have asked (based on contextual documentation). If you have reached the maximum number of questions of that type, do not select any more questions of that type. Select another eligible question type.
                                * Question list: 
                                {questionListBuilder}
                                ** Do not invent, rephrase, or create any new questions outside this list.
                                7. Answering style requirements:
                                Please speak in a slow, gentle, warm, and sweet tone. Your voice should sound polite, calm, and caring.
                                Always use friendly language and keep your speaking speed moderate, natural, patient and kind.
                                Current context:
                                {context}
                                The current user's answer is: {userQuestion}
                                """)
        ];
        
        ChatCompletionOptions options = new()
        {
            ResponseModalities = ChatResponseModalities.Text | ChatResponseModalities.Audio,
            AudioOptions = new ChatAudioOptions(new ChatOutputAudioVoice("cedar"), ChatOutputAudioFormat.Wav)
        };
        
        Log.Information("MatchingReasonableNextQuestionAsync system prompt:{@prompt} ", JsonConvert.SerializeObject(messages));

        ChatClient client = new("gpt-audio", _openAiSettings.ApiKey);
        
        ChatCompletion completion = await client.CompleteChatAsync(messages, options, cancellationToken);
        
        Log.Information("MatchingReasonableNextQuestionAsync next question response:{@completion} ", completion);

        return completion.OutputAudio;
    }

    private async Task ConvertAndSendWebSocketMessageAsync(WebSocket webSocket, Guid sessionId, string eventType, string message, string endMessage = null, string firstQuestionPart = null, string firstQuestionPartUrl = null, CancellationToken cancellationToken = default)
    {
        Log.Information("ConvertAndSendWebSocketMessageAsync message:{@message}, endMessage:{@endMessage}", message,endMessage);
        
        var welcomeMessageDto = JsonConvert.DeserializeObject<HrInterViewQuestionsDto>(message);
        
        var endMessageDto = new HrInterViewQuestionsDto();

        if (!string.IsNullOrEmpty(endMessage)) endMessageDto = JsonConvert.DeserializeObject<HrInterViewQuestionsDto>(endMessage);

        Log.Information("ConvertAndSendWebSocketMessageAsync welcomeMessageDto:{@welcomeMessageDto}, endMessage:{@welcomeMessageDto}", message,endMessageDto);
        
        var messageFileUrl = JsonConvert.SerializeObject(string.IsNullOrEmpty(firstQuestionPartUrl)
            ? new List<string> { welcomeMessageDto.Url }
            : new List<string> { firstQuestionPartUrl, welcomeMessageDto.Url });
        
        var welcomeEvent = new HrInterViewQuestionEventDto
        {
            SessionId = sessionId,
            EventType = eventType,
            Message = string.IsNullOrEmpty(firstQuestionPart)? firstQuestionPart + welcomeMessageDto.Question : welcomeMessageDto.Question, 
            MessageFileUrl = messageFileUrl,
            EndMessage = string.IsNullOrEmpty(endMessageDto.Question) ? "" : endMessageDto.Question,
            EndMessageFileUrl = string.IsNullOrEmpty(endMessageDto.Question) ? "" : endMessageDto.Url
        };

        await SendWebSocketMessageAsync(webSocket, welcomeEvent, cancellationToken).ConfigureAwait(false);
        
        Log.Information("ConvertAndSendWebSocketMessageAsync messageFileUrl:{@messageFileUrl}, Message:{@Message}", messageFileUrl,firstQuestionPart + welcomeMessageDto.Question);
        
        await _hrInterViewDataProvider.AddHrInterViewSessionAsync(new HrInterViewSession
        {
            SessionId = sessionId,
            Message = string.IsNullOrEmpty(firstQuestionPart)? firstQuestionPart + welcomeMessageDto.Question : welcomeMessageDto.Question, 
            FileUrl = messageFileUrl,
            QuestionType = HrInterViewSessionQuestionType.Assistant }, cancellationToken:cancellationToken).ConfigureAwait(false);
        
        Log.Information("ConvertAndSendWebSocketMessageAsync endMessageDto:{@endMessageDto}",endMessageDto);
        
        if (!string.IsNullOrEmpty(endMessage))  
            await _hrInterViewDataProvider.AddHrInterViewSessionAsync(new HrInterViewSession
            {
                SessionId = sessionId,
                Message = string.IsNullOrEmpty(endMessageDto.Question) ? "" : endMessageDto.Question,
                FileUrl = JsonConvert.SerializeObject(new List<string>{endMessageDto.Url}),
                QuestionType = HrInterViewSessionQuestionType.Assistant,
                CreatedDate = new DateTimeOffset(new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc))
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
        
        await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        return await GetAudioTextAsync().ConfigureAwait(false);
    }
    
    private async Task<string> UploadFileAsync(byte[] fileBytes, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var response = await _attachmentUtilService.UploadFilesAsync(new List<UploadAttachmentDto> { new() { FileContent = fileBytes, FileName = $"hr_interview_question_audio_{sessionId}_{Guid.NewGuid()}.wav" } }, cancellationToken).ConfigureAwait(false);

        Log.Information("UploadAndRetryFileAsync response: {@Response}", response);
        
        return response.FirstOrDefault()?.FileUrl;
    }
}