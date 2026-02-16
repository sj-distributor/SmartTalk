using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using AutoMapper;
using Google.Cloud.Translation.V2;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Core.Services.RetrievalDb.VectorDb;
using SmartTalk.Core.Settings.Sales;
using SmartTalk.Core.Services.Twilio;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Restaurant;
using SmartTalk.Messages.Dto.WebSocket;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.STT;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public interface IAiSpeechAssistantProcessJobService : IScopedDependency
{
    Task SyncAiSpeechAssistantInfoToAgentAsync(SyncAiSpeechAssistantInfoToAgentCommand command, CancellationToken cancellationToken);

    Task SyncAiSpeechAssistantLanguageAsync(SyncAiSpeechAssistantLanguageCommand command, CancellationToken cancellationToken);

    Task SyncAiSpeechAssistantKnowledgePromptAsync(SyncAiSpeechAssistantKnowledgePromptCommand command, CancellationToken cancellationToken);
    
    Task RecordAiSpeechAssistantCallAsync(AiSpeechAssistantStreamContextDto context, PhoneOrderRecordType orderRecordType, CancellationToken cancellationToken);
}

public class AiSpeechAssistantProcessJobService : IAiSpeechAssistantProcessJobService
{
    private readonly IMapper _mapper;
    private readonly IVectorDb _vectorDb;
    private readonly ITwilioService _twilioService;
    private readonly TranslationClient _translationClient;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IRestaurantDataProvider _restaurantDataProvider;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly IAiSpeechAssistantDataProvider _speechAssistantDataProvider;
    private readonly ICrmClient _crmClient;
    private readonly IPosDataProvider _posDataProvider;
    private readonly SalesSetting _salesSetting;

    public AiSpeechAssistantProcessJobService(
        IMapper mapper,
        IVectorDb vectorDb,
        ICrmClient crmClient,
        SalesSetting salesSetting,
        ITwilioService twilioService,
        IPosDataProvider posDataProvider,
        TranslationClient translationClient,
        IAgentDataProvider agentDataProvider,
        IRestaurantDataProvider restaurantDataProvider,
        IPhoneOrderDataProvider phoneOrderDataProvider,
        IAiSpeechAssistantDataProvider speechAssistantDataProvider)
    {
        _mapper = mapper;
        _vectorDb = vectorDb;
        _crmClient = crmClient;
        _salesSetting = salesSetting;
        _twilioService = twilioService;
        _posDataProvider = posDataProvider;
        _translationClient = translationClient;
        _agentDataProvider = agentDataProvider;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _restaurantDataProvider = restaurantDataProvider;
        _speechAssistantDataProvider = speechAssistantDataProvider;
    }

    public async Task RecordAiSpeechAssistantCallAsync(AiSpeechAssistantStreamContextDto context, PhoneOrderRecordType orderRecordType, CancellationToken cancellationToken)
    {
        var callInfo = await _twilioService.FetchCallAsync(context.CallSid).ConfigureAwait(false);
        
        var existRecord = await _phoneOrderDataProvider.GetPhoneOrderRecordBySessionIdAsync(context.CallSid, cancellationToken).ConfigureAwait(false);

        if (existRecord != null ) return;
        
        var agentAssistant = await _speechAssistantDataProvider.GetAgentAssistantsAsync(assistantIds: [context.Assistant.Id], cancellationToken: cancellationToken).ConfigureAwait(false);

        if (agentAssistant == null || agentAssistant.Count == 0) throw new Exception("AgentAssistant is null");
        
        var record = new PhoneOrderRecord
        {
            AssistantId = context.Assistant.Id,
            AgentId = agentAssistant.First().AgentId,
            SessionId = context.CallSid,
            Status = PhoneOrderRecordStatus.Transcription,
            Tips = context.ConversationTranscription.FirstOrDefault().Item2,
            TranscriptionText = string.Empty,
            Language = TranscriptionLanguage.Chinese,
            CreatedDate = callInfo.StartTime ?? DateTimeOffset.Now,
            OrderStatus = PhoneOrderOrderStatus.Pending,
            CustomerName = context.UserInfo?.UserName,
            PhoneNumber = context.UserInfo?.PhoneNumber,
            IsTransfer = context.IsTransfer,
            IncomingCallNumber = context.LastUserInfo.PhoneNumber,
            OrderRecordType = orderRecordType
        };

        await _phoneOrderDataProvider.AddPhoneOrderRecordsAsync([record], cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task SyncAiSpeechAssistantInfoToAgentAsync(SyncAiSpeechAssistantInfoToAgentCommand command, CancellationToken cancellationToken)
    {
        var agentAndAssistantPairs = await _speechAssistantDataProvider.GetAgentAndAiSpeechAssistantPairsAsync(cancellationToken).ConfigureAwait(false);
        
        Log.Information("Getting agent and assistant pairs: {AgentAndAssistantPairs}", agentAndAssistantPairs);
        
        foreach (var (agent, assistant) in agentAndAssistantPairs)
        {
            agent.IsSurface = true;
            agent.RelateId = agent.Id;
            agent.Name = assistant.Name;
            agent.Type = AgentType.Agent;
            agent.Voice = assistant.ModelVoice;
            agent.WaitInterval = assistant.WaitInterval;
            agent.IsTransferHuman = assistant.IsTransferHuman;
            agent.Channel = AiSpeechAssistantChannel.PhoneChat;
        }
        
        await _agentDataProvider.UpdateAgentsAsync(agentAndAssistantPairs.Select(x => x.Item1).ToList(), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task SyncAiSpeechAssistantLanguageAsync(SyncAiSpeechAssistantLanguageCommand command, CancellationToken cancellationToken)
    {
        var companyName = _salesSetting.CompanyName?.Trim();
        if (string.IsNullOrWhiteSpace(companyName))
        {
            Log.Information("Skip syncing assistant language: Sales CompanyName is empty.");
            return;
        }

        var company = await _posDataProvider.GetPosCompanyByNameAsync(companyName, cancellationToken).ConfigureAwait(false);
        if (company == null)
        {
            Log.Information("Skip syncing assistant language: company not found: {CompanyName}", companyName);
            return;
        }

        var assistantIds = await _posDataProvider.GetAssistantIdsByCompanyIdAsync(company.Id, cancellationToken).ConfigureAwait(false);
        if (assistantIds.Count == 0) return;

        var assistants = await _speechAssistantDataProvider.GetAiSpeechAssistantByIdsAsync(assistantIds, cancellationToken).ConfigureAwait(false);
        if (assistants.Count == 0) return;

        var crmToken = await _crmClient.GetCrmTokenAsync(cancellationToken).ConfigureAwait(false);
        if (crmToken == null) return;
        
        var updates = new List<Domain.AISpeechAssistant.AiSpeechAssistant>();
        var rateLimitDelay = TimeSpan.FromMinutes(3);

        foreach (var assistant in assistants)
        {
            if (!TryGetCustomerId(assistant, out var customerId)) continue;

            try
            {
                var contacts = await _crmClient.GetCustomerContactsAsync(customerId, crmToken, cancellationToken).ConfigureAwait(false);
                var language = BuildLanguageText(contacts);

                if (!string.Equals(assistant.Language ?? string.Empty, language, StringComparison.Ordinal))
                {
                    assistant.Language = language;
                    updates.Add(assistant);
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Log.Warning(ex, "Rate limited while syncing language for assistant {AssistantId} (CustomerId: {CustomerId})", assistant.Id, customerId);
                await Task.Delay(rateLimitDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to sync language for assistant {AssistantId} (CustomerId: {CustomerId})", assistant.Id, assistant.Name);
            }
        }

        if (updates.Count == 0) return;

        await _speechAssistantDataProvider.UpdateAiSpeechAssistantsAsync(updates, cancellationToken: cancellationToken).ConfigureAwait(false);

        static bool TryGetCustomerId(Domain.AISpeechAssistant.AiSpeechAssistant assistant, out string customerId)
        {
            customerId = null;
            if (string.IsNullOrWhiteSpace(assistant.Name)) return false;

            var rawCustomerId = assistant.Name.Trim();
            var firstSegment = rawCustomerId.Split('/')[0].Trim();
            if (string.IsNullOrEmpty(firstSegment) || !char.IsDigit(firstSegment[0])) return false;

            customerId = firstSegment;
            return true;
        }
    }

    public async Task SyncAiSpeechAssistantKnowledgePromptAsync(SyncAiSpeechAssistantKnowledgePromptCommand command, CancellationToken cancellationToken)
    {
        var activeKnowledges = await _speechAssistantDataProvider.GetAiSpeechAssistantKnowledgesByIsActiveAsync(true, cancellationToken).ConfigureAwait(false);

        if (activeKnowledges.Count == 0) return;

        var knowledgeIds = activeKnowledges.Select(x => x.Id).ToList();
        
        Log.Information("[Job] SyncAiSpeechAssistantKnowledgePrompt. KnowledgeIds={KnowledgeIds}", knowledgeIds);

        var allCopyRelateds = await _speechAssistantDataProvider.GetKnowledgeCopyRelatedByTargetKnowledgeIdAsync(knowledgeIds, null, cancellationToken).ConfigureAwait(false);

        var relatedLookup = (allCopyRelateds ?? new List<AiSpeechAssistantKnowledgeCopyRelated>())
            .GroupBy(x => x.TargetKnowledgeId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.CreatedDate).ThenBy(x => x.Id).ToList());

        var updates = new List<AiSpeechAssistantKnowledge>();

        foreach (var knowledge in activeKnowledges)
        {
            relatedLookup.TryGetValue(knowledge.Id, out var relateds);
            relateds ??= [];

            if (!TryMergeNormalizedJson(knowledge.Id, knowledge.Json, relateds.Select(x => x.CopyKnowledgePoints), out var mergedJson))
            {
                Log.Warning("Sync knowledge prompt skipped update due to merge failure. KnowledgeId={KnowledgeId}", knowledge.Id);
                continue;
            }

            var newPrompt = GenerateKnowledgePrompt(mergedJson);

            if (!string.Equals(knowledge.Prompt ?? string.Empty, newPrompt, StringComparison.Ordinal))
            {
                knowledge.Prompt = newPrompt;
                updates.Add(knowledge);
            }
        }

        if (updates.Count == 0) return;

        await _speechAssistantDataProvider
            .UpdateAiSpeechAssistantKnowledgesAsync(updates, true, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryMergeNormalizedJson(int knowledgeId, string knowledgeJson, IEnumerable<string> copyKnowledgePoints, out string mergedJson)
    {
        mergedJson = string.Empty;

        if (!TryNormalizeJsonToObject(knowledgeId, knowledgeJson, "knowledge.Json", out var merged))
            return false;

        var relationIndex = 0;
        foreach (var json in copyKnowledgePoints ?? Enumerable.Empty<string>())
        {
            relationIndex++;

            if (!TryNormalizeJsonToObject(knowledgeId, json, $"copyKnowledgePoints[{relationIndex}]", out var normalized))
                return false;

            merged.Merge(normalized, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Concat });
        }

        mergedJson = merged.ToString(Formatting.None);
        return true;
    }

    private static bool TryNormalizeJsonToObject(int knowledgeId, string json, string source, out JObject normalizedObject)
    {
        normalizedObject = new JObject();

        if (string.IsNullOrWhiteSpace(json))
            return true;

        try
        {
            normalizedObject = RemoveCopySuffixFromKeys(JObject.Parse(json));
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Sync knowledge prompt merge failed due to invalid json. KnowledgeId={KnowledgeId}, Source={Source}, Json={Json}",
                knowledgeId, source, json);
            return false;
        }
    }

    private static JObject RemoveCopySuffixFromKeys(JObject source)
    {
        var result = new JObject();

        foreach (var prop in source.Properties())
        {
            var newKey = RemoveCopySuffix(prop.Name);
            result[newKey] = StripCopySuffixFromToken(prop.Value);
        }

        return result;
    }

    private static string RemoveCopySuffix(string key)
    {
        if (key.EndsWith("-副本", StringComparison.Ordinal))
            return key[..^"-副本".Length];

        if (key.EndsWith("副本", StringComparison.Ordinal))
            return key[..^"副本".Length];

        return key;
    }

    private static JToken StripCopySuffixFromToken(JToken token)
    {
        return token.Type switch
        {
            JTokenType.Object => RemoveCopySuffixFromKeys((JObject)token),
            JTokenType.Array => new JArray(token.Select(StripCopySuffixFromToken)),
            _ => token.DeepClone()
        };
    }

    private static string GenerateKnowledgePrompt(string json)
    {
        var prompt = new StringBuilder();
        var jsonData = JObject.Parse(json);
        var textInfo = CultureInfo.InvariantCulture.TextInfo;

        foreach (var property in jsonData.Properties())
        {
            var key = textInfo.ToTitleCase(property.Name);
            var value = property.Value;

            if (value is JArray array)
            {
                var list = array.Select((item, index) => $"{index + 1}. {item.ToString()}").ToList();
                prompt.AppendLine($"{key}：\n{string.Join("\n", list)}\n");
            }
            else
                prompt.AppendLine($"{key}： {value}\n");
        }

        return prompt.ToString();
    }

    private static string BuildLanguageText(IReadOnlyList<SmartTalk.Messages.Dto.Crm.CrmContactDto> contacts)
    {
        if (contacts == null || contacts.Count == 0) return string.Empty;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var contact in contacts)
        {
            var language = contact.Language?.Trim();
            if (string.IsNullOrWhiteSpace(language)) continue;
            if (!seen.Add(language)) continue;

            result.Add(language);
        }

        return result.Count == 0 ? string.Empty : string.Join("/", result);
    }

    private static string FormattedConversation(List<(AiSpeechAssistantSpeaker, string)> conversationTranscription)
    {
        var formattedConversation = new StringBuilder();

        foreach (var entry in conversationTranscription)
        {
            var speaker = entry.Item1 == AiSpeechAssistantSpeaker.Ai ? "Restaurant" : "Customer";
            formattedConversation.AppendLine($"{speaker}: {entry.Item2}");
        }

        return formattedConversation.ToString();
    }

    private static List<PhoneOrderConversation> ConvertToPhoneOrderConversations(List<(AiSpeechAssistantSpeaker, string)> conversationTranscription, int recordId)
    {
        var conversations = new List<PhoneOrderConversation>();
        if (conversationTranscription == null || !conversationTranscription.Any()) return conversations;

        var order = 0;
        PhoneOrderConversation currentConversation = null;

        for (var i = 0; i < conversationTranscription.Count; i++)
        {
            var entry = conversationTranscription[i];
            var currentSpeaker = entry.Item1;
            var currentText = entry.Item2;

            if (currentConversation == null)
            {
                if (currentSpeaker == AiSpeechAssistantSpeaker.Ai)
                {
                    currentConversation = new PhoneOrderConversation
                    {
                        RecordId = recordId,
                        Question = currentText,
                        Answer = string.Empty,
                        Order = order++
                    };
                }
            }
            else
            {
                switch (currentSpeaker)
                {
                    case AiSpeechAssistantSpeaker.User when conversationTranscription[i - 1].Item1 == AiSpeechAssistantSpeaker.Ai:
                        currentConversation.Answer = currentText;
                        break;
                    case AiSpeechAssistantSpeaker.Ai when conversationTranscription[i - 1].Item1 == AiSpeechAssistantSpeaker.User:
                        conversations.Add(currentConversation);
                        currentConversation = new PhoneOrderConversation
                        {
                            RecordId = recordId,
                            Question = currentText,
                            Answer = string.Empty,
                            Order = order++
                        };
                        break;
                    case AiSpeechAssistantSpeaker.Ai:
                        currentConversation.Question += " " + currentText;
                        break;
                    default:
                        currentConversation.Answer += " " + currentText;
                        break;
                }
            }
        }

        if (currentConversation != null) conversations.Add(currentConversation);

        return conversations;
    }

    private async Task OrderRestaurantItemsAsync(PhoneOrderRecord record, AiSpeechAssistantOrderDto foods, CancellationToken cancellationToken)
    {
        var items = await MatchSimilarRestaurantItemsAsync(record, foods, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Matched similar restaurant items: {@items}", items);
        
        if (items.Count != 0)
            await _phoneOrderDataProvider.AddPhoneOrderItemAsync(items, true, cancellationToken).ConfigureAwait(false);
    }
    
    private async Task<List<PhoneOrderOrderItem>> MatchSimilarRestaurantItemsAsync(PhoneOrderRecord record, AiSpeechAssistantOrderDto foods, CancellationToken cancellationToken)
    {
        var result = new PhoneOrderDetailDto { FoodDetails = new List<FoodDetailDto>() };
        var restaurant = await _restaurantDataProvider.GetRestaurantByAgentIdAsync(record.AgentId, cancellationToken).ConfigureAwait(false);

        var tasks = _mapper.Map<PhoneOrderDetailDto>(foods).FoodDetails.Select(async foodDetail =>
        {
            var similarFoodsResponse = await _vectorDb.GetSimilarListAsync(
                restaurant.Id.ToString(), foodDetail.FoodName, minRelevance: 0.4, cancellationToken: cancellationToken).ToListAsync(cancellationToken);

            if (similarFoodsResponse.Count == 0) return null;
            
            var payload = similarFoodsResponse.First().Item1.Payload[VectorDbStore.ReservedRestaurantPayload].ToString();
            
            if (string.IsNullOrEmpty(payload)) return null;
            
            foodDetail.FoodName = JsonConvert.DeserializeObject<RestaurantPayloadDto>(payload).Name;
            foodDetail.Price = (double)JsonConvert.DeserializeObject<RestaurantPayloadDto>(payload).Price;
            foodDetail.ProductId = JsonConvert.DeserializeObject<RestaurantPayloadDto>(payload).ProductId;
            
            return foodDetail;
        }).ToList();

        var completedTasks = await Task.WhenAll(tasks);
        
        result.FoodDetails.AddRange(completedTasks.Where(fd => fd != null));
        
        return result.FoodDetails.Select(x => new PhoneOrderOrderItem
        {
            RecordId = record.Id,
            FoodName = x.FoodName,
            Quantity = int.TryParse(x.Count, out var parsedValue) ? parsedValue : 1,
            Price = x.Price,
            Note = x.Remark,
            ProductId = x.ProductId
        }).ToList();
    }
}
