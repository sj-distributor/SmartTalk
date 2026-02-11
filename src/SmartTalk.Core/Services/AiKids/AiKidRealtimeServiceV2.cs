using System.Text;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.RealtimeAiV2.Services;
using SmartTalk.Messages.Commands.AiKids;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Dto.Smarties;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.Hr;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.AiKids;

public interface IAiKidRealtimeServiceV2 : IScopedDependency
{
    Task RealtimeAiConnectAsync(AiKidRealtimeCommand command, CancellationToken cancellationToken);
}

public class AiKidRealtimeServiceV2 : IAiKidRealtimeServiceV2
{
    private readonly ISmartiesClient _smartiesClient;
    private readonly IAttachmentService _attachmentService;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    private readonly IRealtimeAiService _realtimeAiService;

    public AiKidRealtimeServiceV2(
        ISmartiesClient smartiesClient,
        IAttachmentService attachmentService,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider,
        IRealtimeAiService realtimeAiService)
    {
        _smartiesClient = smartiesClient;
        _attachmentService = attachmentService;
        _backgroundJobClient = backgroundJobClient;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _realtimeAiService = realtimeAiService;
    }

    public async Task RealtimeAiConnectAsync(AiKidRealtimeCommand command, CancellationToken cancellationToken)
    {
        var assistant = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantWithKnowledgeAsync(command.AssistantId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not find assistant by id: {command.AssistantId}");

        var timer = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantTimerByAssistantIdAsync(assistant.Id, cancellationToken).ConfigureAwait(false);
        assistant.Timer = timer;

        var modelConfig = await BuildModelConfigAsync(assistant, cancellationToken).ConfigureAwait(false);

        var greetings = assistant.Knowledge?.Greetings;
        var orderRecordType = command.OrderRecordType;
        var assistantId = assistant.Id;

        var options = new RealtimeSessionOptions
        {
            ModelConfig = modelConfig,
            ConnectionProfile = new RealtimeAiConnectionProfile
            {
                ProfileId = assistant.Id.ToString()
            },
            WebSocket = command.WebSocket,
            InputFormat = command.InputFormat,
            OutputFormat = command.OutputFormat,
            Region = command.Region,
            EnableRecording = true,
            IdleFollowUp = timer != null
                ? new RealtimeSessionIdleFollowUp
                {
                    TimeoutSeconds = timer.TimeSpanSeconds,
                    FollowUpMessage = timer.AlterContent,
                    SkipRounds = timer.SkipRound
                }
                : null,
            OnSessionReadyAsync = async sendText =>
            {
                if (!string.IsNullOrEmpty(greetings))
                    await sendText($"Greet the user with: {greetings}").ConfigureAwait(false);
            },
            OnRecordingCompleteAsync = async (sessionId, wavBytes) =>
            {
                var audio = await _attachmentService.UploadAttachmentAsync(
                    new UploadAttachmentCommand
                    {
                        Attachment = new UploadAttachmentDto
                        {
                            FileName = Guid.NewGuid() + ".wav",
                            FileContent = wavBytes
                        }
                    }, CancellationToken.None).ConfigureAwait(false);

                Log.Information("[AiKidRealtimeV2] Audio uploaded, SessionId: {SessionId}, AssistantId: {AssistantId}, Url: {Url}",
                    sessionId, assistantId, audio?.Attachment?.FileUrl);

                if (!string.IsNullOrEmpty(audio?.Attachment?.FileUrl) && assistantId != 0)
                {
                    _backgroundJobClient.Enqueue<IAiKidRealtimeProcessJobService>(x =>
                        x.RecordingRealtimeAiAsync(audio.Attachment.FileUrl, assistantId, sessionId, orderRecordType, CancellationToken.None));
                }
            },
            OnTranscriptionsReadyAsync = async (sessionId, transcriptions) =>
            {
                var kid = await _aiSpeechAssistantDataProvider
                    .GetAiKidAsync(agentId: assistant.AgentId, cancellationToken: cancellationToken).ConfigureAwait(false);

                if (kid == null) return;

                _backgroundJobClient.Enqueue<ISmartiesClient>(x =>
                    x.CallBackSmartiesAiKidConversationsAsync(new AiKidConversationCallBackRequestDto
                    {
                        Uuid = kid.KidUuid,
                        SessionId = sessionId,
                        Transcriptions = transcriptions.Select(t => new RealtimeAiTranscriptionDto
                        {
                            Speaker = t.Speaker,
                            Transcription = t.Text
                        }).ToList()
                    }, CancellationToken.None));
            }
        };

        await _realtimeAiService.StartAsync(options, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RealtimeAiModelConfig> BuildModelConfigAsync(
        Domain.AISpeechAssistant.AiSpeechAssistant assistant, CancellationToken cancellationToken)
    {
        var resolvedPrompt = await BuildResolvedPromptAsync(assistant, cancellationToken).ConfigureAwait(false);

        var functionCalls = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantFunctionCallByAssistantIdsAsync(
                [assistant.Id], assistant.ModelProvider, true, cancellationToken).ConfigureAwait(false);

        var configs = functionCalls
            .Where(x => !string.IsNullOrWhiteSpace(x.Content))
            .Select(x => (x.Type, Config: JsonConvert.DeserializeObject<object>(x.Content)))
            .ToList();

        return new RealtimeAiModelConfig
        {
            Provider = assistant.ModelProvider,
            ServiceUrl = assistant.ModelUrl,
            Voice = assistant.ModelVoice,
            ModelName = assistant.ModelName,
            ModelLanguage = assistant.ModelLanguage,
            Prompt = resolvedPrompt,
            Tools = configs
                .Where(x => x.Type == AiSpeechAssistantSessionConfigType.Tool)
                .Select(x => x.Config)
                .ToList(),
            TurnDetection = configs.FirstOrDefault(x => x.Type == AiSpeechAssistantSessionConfigType.TurnDirection).Config,
            InputAudioNoiseReduction = configs.FirstOrDefault(x => x.Type == AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction).Config
        };
    }

    private async Task<string> BuildResolvedPromptAsync(
        Domain.AISpeechAssistant.AiSpeechAssistant assistant, CancellationToken cancellationToken)
    {
        if (assistant?.Knowledge == null || string.IsNullOrEmpty(assistant.Knowledge.Prompt))
            return string.Empty;

        var pstTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        var currentTime = pstTime.ToString("yyyy-MM-dd HH:mm:ss");

        var finalPrompt = assistant.Knowledge.Prompt
            .Replace("#{current_time}", currentTime)
            .Replace("#{pst_date}", $"{pstTime.Date:yyyy-MM-dd} {pstTime.DayOfWeek}");

        if (finalPrompt.Contains("#{restaurant_info}") || finalPrompt.Contains("#{restaurant_items}"))
        {
            var aiKid = await _aiSpeechAssistantDataProvider
                .GetAiKidAsync(agentId: assistant.AgentId, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (aiKid != null)
            {
                try
                {
                    var response = await _smartiesClient
                        .GetCrmCustomerInfoAsync(aiKid.KidUuid, cancellationToken).ConfigureAwait(false);

                    Log.Information("Get crm customer info response: {@Response}", response);

                    var result = SplicingCrmCustomerResponse(response?.Data?.FirstOrDefault());
                    finalPrompt = finalPrompt
                        .Replace("#{restaurant_info}", result.RestaurantInfo)
                        .Replace("#{restaurant_items}", result.PurchasedItems);
                }
                catch (Exception e)
                {
                    Log.Warning("Replace restaurant info failed: {Exception}", e);
                }
            }
        }

        if (finalPrompt.Contains("#{hr_interview_section1}", StringComparison.OrdinalIgnoreCase))
        {
            var cacheKeys = Enum.GetValues(typeof(HrInterviewQuestionSection))
                .Cast<HrInterviewQuestionSection>()
                .Select(section => "hr_interview_" + section.ToString().ToLower())
                .ToList();

            var caches = await _aiSpeechAssistantDataProvider
                .GetAiSpeechAssistantKnowledgeVariableCachesAsync(cacheKeys, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            foreach (var section in Enum.GetValues(typeof(HrInterviewQuestionSection)).Cast<HrInterviewQuestionSection>())
            {
                var cacheKey = $"hr_interview_{section.ToString().ToLower()}";
                var placeholder = $"#{{{cacheKey}}}";
                finalPrompt = finalPrompt.Replace(placeholder, caches.FirstOrDefault(x => x.CacheKey == cacheKey)?.CacheValue);
            }
        }

        if (finalPrompt.Contains("#{hr_interview_questions}", StringComparison.OrdinalIgnoreCase))
        {
            var cache = await _aiSpeechAssistantDataProvider
                .GetAiSpeechAssistantKnowledgeVariableCachesAsync(["hr_interview_questions"], cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            finalPrompt = finalPrompt.Replace("#{hr_interview_questions}", cache.FirstOrDefault()?.CacheValue);
        }

        Log.Information("The final prompt: {FinalPrompt}", finalPrompt);

        return finalPrompt;
    }

    private static (string RestaurantInfo, string PurchasedItems) SplicingCrmCustomerResponse(CrmCustomerInfoDto customerInfo)
    {
        if (customerInfo == null) return (string.Empty, string.Empty);

        var infoSb = new StringBuilder();
        var itemsSb = new StringBuilder();

        infoSb.AppendLine($"餐厅名字：{customerInfo.Name}");
        infoSb.AppendLine($"餐厅地址：{customerInfo.Address}");

        itemsSb.AppendLine("餐厅购买过的items（餐厅所需要的）：");

        var idx = 1;
        foreach (var product in customerInfo.Products.OrderByDescending(x => x.CreatedAt))
        {
            var itemName = product.Name;
            var specSb = new StringBuilder();
            foreach (var attr in product.Attributes)
            {
                var attrName = attr.Name;
                var options = attr.Options;
                var optionNames = string.Join("、", options.Select(opt => opt.Name.ToString()));
                specSb.Append($"{attrName}: {optionNames}; ");
            }

            if (idx < 4)
                itemsSb.AppendLine($"{idx}. {itemName}(新品)，规格: {specSb.ToString().Trim()}");
            else
                itemsSb.AppendLine($"{idx}. {itemName}，规格: {specSb.ToString().Trim()}");

            idx++;
        }

        return (infoSb.ToString(), itemsSb.ToString());
    }
}
