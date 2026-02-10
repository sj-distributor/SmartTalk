using System.Text;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.RealtimeAi.Services;
using SmartTalk.Messages.Commands.AiKids;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Dto.Smarties;
using SmartTalk.Messages.Enums.Hr;

namespace SmartTalk.Core.Services.AiKids;

public interface IAiKidRealtimeService : IScopedDependency
{
    Task RealtimeAiConnectAsync(AiKidRealtimeCommand command, CancellationToken cancellationToken);
}

public class AiKidRealtimeService : IAiKidRealtimeService
{
    private readonly ISmartiesClient _smartiesClient;
    private readonly IRealtimeAiService _realtimeAiService;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    public AiKidRealtimeService(
        ISmartiesClient smartiesClient,
        IRealtimeAiService realtimeAiService,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _smartiesClient = smartiesClient;
        _realtimeAiService = realtimeAiService;
        _backgroundJobClient = backgroundJobClient;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
    }

    public async Task RealtimeAiConnectAsync(AiKidRealtimeCommand command, CancellationToken cancellationToken)
    {
        var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantWithKnowledgeAsync(command.AssistantId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not find assistant by id: {command.AssistantId}");

        var timer = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantTimerByAssistantIdAsync(assistant.Id, cancellationToken).ConfigureAwait(false);
        assistant.Timer = timer;

        var finalPrompt = await BuildingAiSpeechAssistantKnowledgeBaseAsync(assistant, cancellationToken).ConfigureAwait(false);
        var initialPrompt = string.IsNullOrWhiteSpace(finalPrompt) ? "You are a friendly assistant" : finalPrompt;

        var options = new RealtimeSessionOptions
        {
            WebSocket = command.WebSocket,
            AssistantProfile = assistant,
            InitialPrompt = initialPrompt,
            InputFormat = command.InputFormat,
            OutputFormat = command.OutputFormat,
            Region = command.Region
        };

        var orderRecordType = command.OrderRecordType;

        var callbacks = new RealtimeSessionCallbacks
        {
            OnRecordingSavedAsync = (fileUrl, sessionId) =>
            {
                if (!string.IsNullOrEmpty(fileUrl) && assistant.Id != 0)
                {
                    _backgroundJobClient.Enqueue<IAiKidRealtimeProcessJobService>(x =>
                        x.RecordingRealtimeAiAsync(fileUrl, assistant.Id, sessionId, orderRecordType, CancellationToken.None));
                }

                return Task.CompletedTask;
            },
            OnTranscriptionsReadyAsync = async (sessionId, transcriptions) =>
            {
                var kid = await _aiSpeechAssistantDataProvider.GetAiKidAsync(agentId: assistant.AgentId).ConfigureAwait(false);
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
            },
            IdleFollowUp = timer != null
                ? new RealtimeSessionIdleFollowUp
                {
                    TimeoutSeconds = timer.TimeSpanSeconds,
                    FollowUpMessage = timer.AlterContent,
                    SkipRounds = timer.SkipRound
                }
                : null
        };

        await _realtimeAiService.StartAsync(options, callbacks, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> BuildingAiSpeechAssistantKnowledgeBaseAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistant, CancellationToken cancellationToken)
    {
        if (assistant?.Knowledge == null || string.IsNullOrEmpty(assistant.Knowledge?.Prompt)) return string.Empty;

        var pstTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        var currentTime = pstTime.ToString("yyyy-MM-dd HH:mm:ss");

        var finalPrompt = assistant.Knowledge.Prompt
            .Replace("#{current_time}", currentTime)
            .Replace("#{pst_date}", $"{pstTime.Date:yyyy-MM-dd} {pstTime.DayOfWeek}");

        if (finalPrompt.Contains("#{restaurant_info}") || finalPrompt.Contains("#{restaurant_items}"))
        {
            var aiKid = await _aiSpeechAssistantDataProvider.GetAiKidAsync(agentId: assistant.AgentId, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (aiKid != null)
            {
                try
                {
                    var response = await _smartiesClient.GetCrmCustomerInfoAsync(aiKid.KidUuid, cancellationToken).ConfigureAwait(false);

                    Log.Information("Get crm customer info response: {@Response}", response);

                    var result = SplicingCrmCustomerResponse(response?.Data?.FirstOrDefault());

                    finalPrompt = finalPrompt.Replace("#{restaurant_info}", result.RestaurantInfo).Replace("#{restaurant_items}", result.PurchasedItems);
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

        Log.Information($"The final prompt: {finalPrompt}");

        return finalPrompt;
    }

    private (string RestaurantInfo, string PurchasedItems) SplicingCrmCustomerResponse(CrmCustomerInfoDto customerInfo)
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