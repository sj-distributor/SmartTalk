using Newtonsoft.Json;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Domain.System;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.Messages.Enums.Sales;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService
{
    Task<GetAiSpeechAssistantKnowledgeCapabilitiesResponse> GetAiSpeechAssistantKnowledgeCapabilitiesAsync(
        GetAiSpeechAssistantKnowledgeCapabilitiesRequest request,
        CancellationToken cancellationToken);

    Task<UpdateAiSpeechAssistantKnowledgeCapabilitiesResponse> UpdateAiSpeechAssistantKnowledgeCapabilitiesAsync(
        UpdateAiSpeechAssistantKnowledgeCapabilitiesCommand command,
        CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantService
{
    private const string InputAudioNoiseReductionName = "input_audio_noise_reduction";
    private const string DefaultNoiseReductionType = "near_field";

    public async Task<GetAiSpeechAssistantKnowledgeCapabilitiesResponse> GetAiSpeechAssistantKnowledgeCapabilitiesAsync(
        GetAiSpeechAssistantKnowledgeCapabilitiesRequest request,
        CancellationToken cancellationToken)
    {
        return new GetAiSpeechAssistantKnowledgeCapabilitiesResponse
        {
            Data = await BuildKnowledgeCapabilitiesDataAsync(request.StoreId, request.Keyword, cancellationToken).ConfigureAwait(false)
        };
    }

    public async Task<UpdateAiSpeechAssistantKnowledgeCapabilitiesResponse> UpdateAiSpeechAssistantKnowledgeCapabilitiesAsync(
        UpdateAiSpeechAssistantKnowledgeCapabilitiesCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Items == null || command.Items.Count == 0)
            throw new ArgumentException("Capability items are required", nameof(command.Items));

        var context = await LoadKnowledgeCapabilityContextAsync(command.StoreId, cancellationToken).ConfigureAwait(false);

        if (!context.CanConfigure)
            throw new InvalidOperationException("The store is not linked to Hifood/CRM.");

        foreach (var item in command.Items)
        {
            if (item.AssistantId <= 0)
                throw new ArgumentException("AssistantId is required", nameof(command.Items));

            var record = context.Records.FirstOrDefault(x => x.Assistant.Id == item.AssistantId);

            if (record == null)
                throw new InvalidOperationException($"Knowledge capability not found. AssistantId: {item.AssistantId}");

            await ApplyKnowledgeCapabilityAsync(record, item, cancellationToken).ConfigureAwait(false);
        }

        return new UpdateAiSpeechAssistantKnowledgeCapabilitiesResponse
        {
            Data = await BuildKnowledgeCapabilitiesDataAsync(command.StoreId, null, cancellationToken).ConfigureAwait(false)
        };
    }

    private async Task<GetAiSpeechAssistantKnowledgeCapabilitiesResponseData> BuildKnowledgeCapabilitiesDataAsync(
        int storeId,
        string keyword,
        CancellationToken cancellationToken)
    {
        var context = await LoadKnowledgeCapabilityContextAsync(storeId, cancellationToken).ConfigureAwait(false);

        if (!context.CanConfigure)
        {
            return new GetAiSpeechAssistantKnowledgeCapabilitiesResponseData
            {
                CanConfigure = false,
                Capabilities = []
            };
        }

        var assistantIds = context.Records.Select(x => x.Assistant.Id).Distinct().ToList();
        var functionCalls = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallsAsync(
            assistantIds,
            [OpenAiToolConstants.RepeatOrder, OpenAiToolConstants.SatisfyOrder],
            AiSpeechAssistantSessionConfigType.Tool,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var allSales = await _salesDataProvider.GetAllSalesAsync(cancellationToken).ConfigureAwait(false);

        var capabilities = context.Records
            .Select(x => BuildKnowledgeCapabilityDto(x, functionCalls, allSales))
            .ToList();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            capabilities = capabilities.Where(x =>
                    (x.KnowledgeName ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    (x.AssistantName ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return new GetAiSpeechAssistantKnowledgeCapabilitiesResponseData
        {
            CanConfigure = true,
            Capabilities = capabilities
                .OrderBy(x => x.AssistantName)
                .ThenBy(x => x.KnowledgeId)
                .ToList()
        };
    }

    private async Task<KnowledgeCapabilityContext> LoadKnowledgeCapabilityContextAsync(
        int storeId,
        CancellationToken cancellationToken)
    {
        await EnsureStorePermissionAsync(storeId, cancellationToken).ConfigureAwait(false);

        var store = await _posDataProvider.GetPosCompanyStoreAsync(id: storeId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (store == null) throw new InvalidOperationException($"Store not found. StoreId: {storeId}");

        var posAgents = await _posDataProvider.GetPosAgentsAsync(storeIds: [storeId], cancellationToken: cancellationToken).ConfigureAwait(false);
        var agentIds = posAgents.Select(x => x.AgentId).Distinct().ToList();

        if (agentIds.Count == 0)
        {
            return new KnowledgeCapabilityContext
            {
                Store = store,
                CanConfigure = true,
                Records = []
            };
        }

        var agents = await _agentDataProvider.GetAgentsByIdsAsync(agentIds, cancellationToken).ConfigureAwait(false);
        var agentMap = agents.ToDictionary(x => x.Id);
        var agentAssistants = await _aiSpeechAssistantDataProvider.GetAgentAssistantsAsync(agentIds: agentIds, cancellationToken: cancellationToken).ConfigureAwait(false);
        var assistantIds = agentAssistants.Select(x => x.AssistantId).Distinct().ToList();
        var assistants = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdsAsync(assistantIds, cancellationToken).ConfigureAwait(false);
        var assistantMap = assistants.Where(x => x.IsDisplay).ToDictionary(x => x.Id);
        var activeKnowledges = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantActiveKnowledgesAsync(assistantIds, cancellationToken).ConfigureAwait(false);
        var knowledgeByAssistantId = activeKnowledges
            .GroupBy(x => x.AssistantId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(k => k.CreatedDate).First());

        var records = new List<KnowledgeCapabilityRecord>();

        foreach (var agentAssistant in agentAssistants)
        {
            if (!agentMap.TryGetValue(agentAssistant.AgentId, out var agent)) continue;
            if (!assistantMap.TryGetValue(agentAssistant.AssistantId, out var assistant)) continue;
            if (!knowledgeByAssistantId.TryGetValue(assistant.Id, out var knowledge)) continue;

            records.Add(new KnowledgeCapabilityRecord
            {
                Agent = agent,
                Assistant = assistant,
                Knowledge = knowledge
            });
        }

        return new KnowledgeCapabilityContext
        {
            Store = store,
            CanConfigure = true,
            Records = records
        };
    }

    private async Task EnsureStorePermissionAsync(int storeId, CancellationToken cancellationToken)
    {
        var storeUsers = await _posDataProvider.GetPosStoreUsersByUserIdAsync(_currentUser.Id.Value, cancellationToken).ConfigureAwait(false);

        if (storeUsers.All(x => x.StoreId != storeId))
            throw new Exception("Do not have the store permission to operate");
    }

    private AiSpeechAssistantKnowledgeCapabilityDto BuildKnowledgeCapabilityDto(
        KnowledgeCapabilityRecord record,
        List<AiSpeechAssistantFunctionCall> functionCalls,
        List<Sales> sales)
    {
        var repeatOrderEnabled = IsFunctionCallActive(functionCalls, record.Assistant, OpenAiToolConstants.RepeatOrder) &&
                                 IsFunctionCallActive(functionCalls, record.Assistant, OpenAiToolConstants.SatisfyOrder) &&
                                 record.Assistant.ManualRecordWholeAudio;

        var hifoodDataEnabled = IsHifoodDataEnabled(record.Agent, record.Assistant, sales);

        return new AiSpeechAssistantKnowledgeCapabilityDto
        {
            KnowledgeId = record.Knowledge.Id,
            AssistantId = record.Assistant.Id,
            AgentId = record.Agent.Id,
            KnowledgeName = BuildKnowledgeDisplayName(record.Assistant, record.Knowledge),
            AssistantName = record.Assistant.Name,
            HifoodDataEnabled = hifoodDataEnabled,
            RepeatOrderEnabled = repeatOrderEnabled,
            OrderPushHifoodEnabled = record.Assistant.IsAllowOrderPush && record.Assistant.IsAutoGenerateOrder
        };
    }

    private static string BuildKnowledgeDisplayName(
        Domain.AISpeechAssistant.AiSpeechAssistant assistant,
        AiSpeechAssistantKnowledge knowledge)
    {
        return !string.IsNullOrWhiteSpace(assistant.Name)
            ? assistant.Name
            : string.IsNullOrWhiteSpace(knowledge.Brief)
                ? $"Knowledge {knowledge.Id}"
                : knowledge.Brief;
    }

    private static bool IsFunctionCallActive(
        List<AiSpeechAssistantFunctionCall> functionCalls,
        Domain.AISpeechAssistant.AiSpeechAssistant assistant,
        string name)
    {
        return functionCalls.Any(x =>
            x.AssistantId == assistant.Id &&
            x.ModelProvider == assistant.ModelProvider &&
            x.Type == AiSpeechAssistantSessionConfigType.Tool &&
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) &&
            x.IsActive);
    }

    private static bool IsHifoodDataEnabled(
        Agent agent,
        Domain.AISpeechAssistant.AiSpeechAssistant assistant,
        List<Sales> sales)
    {
        var hasHifoodPrompt = HasHifoodDataPrompt(assistant.CustomRecordAnalyzePrompt);

        if (IsSharedCrmAutoSyncSalesAgent(agent))
            return hasHifoodPrompt;

        if (hasHifoodPrompt) return true;

        if (agent.Type != AgentType.Sales || !agent.RelateId.HasValue) return false;

        var relatedSales = sales.FirstOrDefault(x => x.Id == agent.RelateId.Value);

        return relatedSales != null &&
               string.Equals(relatedSales.Name?.Trim(), assistant.Name?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task ApplyKnowledgeCapabilityAsync(
        KnowledgeCapabilityRecord record,
        UpdateAiSpeechAssistantKnowledgeCapabilityDto item,
        CancellationToken cancellationToken)
    {
        if (item.HifoodDataEnabled.HasValue)
            await ApplyHifoodDataCapabilityAsync(record, item.HifoodDataEnabled.Value, cancellationToken).ConfigureAwait(false);

        var shouldUpdateAssistant = false;

        if (item.HifoodDataEnabled.HasValue)
        {
            var prompt = item.HifoodDataEnabled.Value
                ? EnsureRecordAnalyzePrompt(record.Assistant.CustomRecordAnalyzePrompt, record.Assistant, record.Knowledge)
                : RemoveHifoodDataFromRecordAnalyzePrompt(record.Assistant.CustomRecordAnalyzePrompt);
            if (!string.Equals(record.Assistant.CustomRecordAnalyzePrompt, prompt, StringComparison.Ordinal))
            {
                record.Assistant.CustomRecordAnalyzePrompt = prompt;
                shouldUpdateAssistant = true;
            }
        }

        if (item.RepeatOrderEnabled.HasValue)
        {
            record.Assistant.ManualRecordWholeAudio = item.RepeatOrderEnabled.Value;
            shouldUpdateAssistant = true;

            if (item.RepeatOrderEnabled.Value && string.IsNullOrWhiteSpace(record.Assistant.CustomRepeatOrderPrompt))
            {
                record.Assistant.CustomRepeatOrderPrompt = BuildDefaultRepeatOrderPrompt(GetPreferredLanguage(record.Assistant, record.Knowledge));
            }

            await SetRepeatOrderFunctionCallsAsync(record.Assistant, item.RepeatOrderEnabled.Value, cancellationToken).ConfigureAwait(false);
        }

        if (item.OrderPushHifoodEnabled.HasValue)
        {
            record.Assistant.IsAllowOrderPush = item.OrderPushHifoodEnabled.Value;
            record.Assistant.IsAutoGenerateOrder = item.OrderPushHifoodEnabled.Value;
            shouldUpdateAssistant = true;
        }

        if (shouldUpdateAssistant)
            await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantsAsync([record.Assistant], cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task EnableDefaultKnowledgeCapabilitiesIfRequiredAsync(
        Domain.AISpeechAssistant.AiSpeechAssistant assistant,
        AiSpeechAssistantKnowledge knowledge,
        CancellationToken cancellationToken)
    {
        var agent = (await _agentDataProvider.GetAgentsByIdsAsync([assistant.AgentId], cancellationToken).ConfigureAwait(false))
            .FirstOrDefault();
        if (agent == null) return;

        var store = await _posDataProvider.GetPosStoreByAgentIdAsync(agent.Id, cancellationToken).ConfigureAwait(false);
        if (store == null) return;

        var record = new KnowledgeCapabilityRecord
        {
            Agent = agent,
            Assistant = assistant,
            Knowledge = knowledge
        };

        await EnableDefaultKnowledgeCapabilitiesAsync(record, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnableDefaultKnowledgeCapabilitiesAsync(
        KnowledgeCapabilityRecord record,
        CancellationToken cancellationToken)
    {
        await ApplyHifoodDataCapabilityAsync(record, true, cancellationToken).ConfigureAwait(false);

        var shouldUpdateAssistant = false;
        var recordAnalyzePrompt = EnsureRecordAnalyzePrompt(record.Assistant.CustomRecordAnalyzePrompt, record.Assistant, record.Knowledge);
        if (!string.Equals(record.Assistant.CustomRecordAnalyzePrompt, recordAnalyzePrompt, StringComparison.Ordinal))
        {
            record.Assistant.CustomRecordAnalyzePrompt = recordAnalyzePrompt;
            shouldUpdateAssistant = true;
        }

        if (!record.Assistant.ManualRecordWholeAudio)
        {
            record.Assistant.ManualRecordWholeAudio = true;
            shouldUpdateAssistant = true;
        }

        if (string.IsNullOrWhiteSpace(record.Assistant.CustomRepeatOrderPrompt))
        {
            record.Assistant.CustomRepeatOrderPrompt = BuildDefaultRepeatOrderPrompt(
                GetPreferredLanguage(record.Assistant, record.Knowledge));
            shouldUpdateAssistant = true;
        }

        if (!record.Assistant.IsAllowOrderPush)
        {
            record.Assistant.IsAllowOrderPush = true;
            shouldUpdateAssistant = true;
        }

        if (!record.Assistant.IsAutoGenerateOrder)
        {
            record.Assistant.IsAutoGenerateOrder = true;
            shouldUpdateAssistant = true;
        }

        await SetRepeatOrderFunctionCallsAsync(record.Assistant, true, cancellationToken).ConfigureAwait(false);

        if (shouldUpdateAssistant)
            await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantsAsync([record.Assistant], cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyHifoodDataCapabilityAsync(
        KnowledgeCapabilityRecord record,
        bool enabled,
        CancellationToken cancellationToken)
    {
        if (enabled)
        {
            var sales = await GetOrCreateSalesAsync(record.Assistant.Name, cancellationToken).ConfigureAwait(false);

            if (IsSharedCrmAutoSyncSalesAgent(record.Agent))
                return;

            record.Agent.Type = AgentType.Sales;
            record.Agent.RelateId = sales.Id;
            await _agentDataProvider.UpdateAgentsAsync([record.Agent], cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        if (IsSharedCrmAutoSyncSalesAgent(record.Agent))
            return;

        var relatedSales = record.Agent.RelateId.HasValue
            ? (await _salesDataProvider.GetAllSalesAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(x => x.Id == record.Agent.RelateId.Value)
            : null;

        if (record.Agent.Type == AgentType.Sales &&
            relatedSales != null &&
            string.Equals(relatedSales.Name?.Trim(), record.Assistant.Name?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            record.Agent.Type = AgentType.Agent;
            record.Agent.RelateId = record.Assistant.Id;

            await _agentDataProvider.UpdateAgentsAsync([record.Agent], cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Sales> GetOrCreateSalesAsync(string assistantName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assistantName))
            throw new InvalidOperationException("Assistant name is required to enable Hifood data integration.");

        var sales = await _salesDataProvider
            .GetCallInSalesByNameAsync(assistantName.Trim(), null, cancellationToken)
            .ConfigureAwait(false);

        if (sales != null) return sales;

        sales = new Sales
        {
            Name = assistantName.Trim(),
            Type = SalesCallType.CallIn,
            CreatedBy = _currentUser.Id
        };

        await _salesDataProvider.AddSalesAsync(sales, cancellationToken: cancellationToken).ConfigureAwait(false);

        return sales;
    }

    private string EnsureRecordAnalyzePrompt(
        string currentPrompt,
        Domain.AISpeechAssistant.AiSpeechAssistant assistant,
        AiSpeechAssistantKnowledge knowledge)
    {
        if (string.IsNullOrWhiteSpace(currentPrompt))
            return BuildDefaultRecordAnalyzePrompt(GetPreferredLanguage(assistant, knowledge));

        if (currentPrompt.Contains("#{customer_items}", StringComparison.OrdinalIgnoreCase))
            return currentPrompt;

        return currentPrompt.TrimEnd() + Environment.NewLine + Environment.NewLine +
               "The items ordered by customers may refer to the following list but are not limited to it.: #{customer_items}";
    }

    private static bool HasHifoodDataPrompt(string prompt)
    {
        return !string.IsNullOrWhiteSpace(prompt) &&
               (prompt.Contains("#{customer_items}", StringComparison.OrdinalIgnoreCase) ||
                prompt.Contains("{HiFood_商品_商品数据}", StringComparison.OrdinalIgnoreCase));
    }

    private static string RemoveHifoodDataFromRecordAnalyzePrompt(string currentPrompt)
    {
        if (string.IsNullOrWhiteSpace(currentPrompt)) return currentPrompt;

        var lines = currentPrompt
            .Split(Environment.NewLine)
            .Where(x =>
                !x.Contains("#{customer_items}", StringComparison.OrdinalIgnoreCase) &&
                !x.Contains("{HiFood_商品_商品数据}", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return string.Join(Environment.NewLine, lines).Trim();
    }

    private async Task SetRepeatOrderFunctionCallsAsync(
        Domain.AISpeechAssistant.AiSpeechAssistant assistant,
        bool enabled,
        CancellationToken cancellationToken)
    {
        await UpsertAssistantFunctionCallAsync(
            assistant,
            OpenAiToolConstants.RepeatOrder,
            AiSpeechAssistantSessionConfigType.Tool,
            BuildRepeatOrderToolContent(OpenAiToolConstants.RepeatOrder),
            enabled,
            cancellationToken).ConfigureAwait(false);

        await UpsertAssistantFunctionCallAsync(
            assistant,
            OpenAiToolConstants.SatisfyOrder,
            AiSpeechAssistantSessionConfigType.Tool,
            BuildRepeatOrderToolContent(OpenAiToolConstants.SatisfyOrder),
            enabled,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task SetPhoneNoiseReductionAsync(
        Domain.AISpeechAssistant.AiSpeechAssistant assistant,
        bool enabled,
        CancellationToken cancellationToken)
    {
        var content = JsonConvert.SerializeObject(new { type = DefaultNoiseReductionType });

        await UpsertAssistantFunctionCallAsync(
            assistant,
            InputAudioNoiseReductionName,
            AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction,
            content,
            enabled,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> GetPhoneNoiseReductionEnabledAsync(int assistantId, RealtimeAiProvider provider, CancellationToken cancellationToken)
    {
        var functionCalls = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallsAsync(
            [assistantId],
            [InputAudioNoiseReductionName],
            AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction,
            provider,
            true,
            cancellationToken).ConfigureAwait(false);

        return functionCalls.Any();
    }

    private async Task UpsertAssistantFunctionCallAsync(
        Domain.AISpeechAssistant.AiSpeechAssistant assistant,
        string name,
        AiSpeechAssistantSessionConfigType type,
        string content,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var existingFunctionCalls = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallsAsync(
            [assistant.Id],
            [name],
            type,
            assistant.ModelProvider,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (existingFunctionCalls.Count == 0)
        {
            var functionCall = new AiSpeechAssistantFunctionCall
            {
                AssistantId = assistant.Id,
                Name = name,
                Content = content,
                Type = type,
                ModelProvider = assistant.ModelProvider,
                IsActive = isActive
            };

            await _aiSpeechAssistantDataProvider
                .AddAiSpeechAssistantFunctionCallsAsync([functionCall], cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        foreach (var functionCall in existingFunctionCalls)
        {
            functionCall.IsActive = isActive;

            if (isActive && string.IsNullOrWhiteSpace(functionCall.Content))
                functionCall.Content = content;
        }

        await _aiSpeechAssistantDataProvider
            .UpdateAiSpeechAssistantFunctionCallAsync(existingFunctionCalls, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static string BuildRepeatOrderToolContent(string name)
    {
        var description = name == OpenAiToolConstants.SatisfyOrder
            ? "Triggered when the customer confirms the current order items are correct and wants the AI to repeat the final order."
            : "Triggered when the customer asks the AI to repeat or confirm the current order items.";

        return JsonConvert.SerializeObject(new
        {
            type = "function",
            name,
            description
        });
    }

    private static string GetPreferredLanguage(
        Domain.AISpeechAssistant.AiSpeechAssistant assistant,
        AiSpeechAssistantKnowledge knowledge)
    {
        return !string.IsNullOrWhiteSpace(assistant.ModelLanguage)
            ? assistant.ModelLanguage
            : !string.IsNullOrWhiteSpace(knowledge?.ModelLanguage)
                ? knowledge.ModelLanguage
                : AiSpeechAssistantMainLanguage.En.ToString();
    }

    private static string BuildDefaultRepeatOrderPrompt(string modelLanguage)
    {
        return NormalizeLanguage(modelLanguage) switch
        {
            "zh" =>
                """
                你是一名電話錄音分析員，只能使用中文回覆，並且可以準確、完整地復述客戶想要下單的商品。請只回覆客戶的下單商品（例如：一箱雞胸肉、兩箱牛肉）：

                請只按照以下格式輸出：「您想訂購：[商品列表]」。
                """,
            "cantonese" =>
                """
                你係一名電話錄音分析員，只可以用廣東話回覆，並且可以準確、完整咁復述客戶想落單嘅商品。請只回覆客戶嘅落單商品（例如：一箱雞胸肉、兩箱牛肉）：

                請只按照以下格式輸出：「你想訂購：[商品列表]」。
                """,
            _ =>
                """
                You are a call recording analyst who can only speaks English and can accurately and completely repeat the items the customer wants to order.Simply reply with the customer's order items(For example: one case of chicken breast, two cases of beef):

                Please output in the following format only: "You wish to order: [List of Items]".
                """
        };
    }

    private static string BuildDefaultRecordAnalyzePrompt(string modelLanguage)
    {
        return NormalizeLanguage(modelLanguage) switch
        {
            "zh" =>
                """
                你是一名電話錄音分析員。你需要根據錄音內容和語氣情緒作出準確分析，並寫出完整分析報告。即使錄音無法辨識，也請輸出完整報告，並在內容摘要中說明錄音缺失或無法理解的原因。如果錄音為空、沒有有效語音或客戶沒有說話，請直接在摘要中寫明沒有識別到有效內容。

                分析報告格式：交談主題：xxx

                - 來電號碼：#{call_from}（必須記錄）

                - 內容摘要：xxx（如果錄音為空、沒有有效語音或客戶沒有說話，請直接在摘要中寫明沒有識別到有效內容）

                - 客人情感與情緒：xxx

                - 待辦：
                1. xxx
                2. xxx

                - 客人下單內容（如果沒有則忽略，需要記錄不同店鋪的訂單，請不要包含規格大小，只需要數量和商品名稱）：
                1. One case of B/IN BREAST 帶骨雞胸
                2. Two boxes of LEG MT S/L 雞脾肉_無皮
                3. Three BEEF EYE ROUND 牛眼肉

                注意：客戶對一個或多個商品重複下單，請再次檢查並與客戶確認。（如果沒有重複下單則忽略）

                客戶下單商品可參考但不限於以下列表：#{customer_items}
                """,
            "cantonese" =>
                """
                你係一名電話錄音分析員。你需要根據錄音內容同語氣情緒作出準確分析，並寫出完整分析報告。即使錄音無法辨識，都請輸出完整報告，並喺內容摘要入面說明錄音缺失或者無法理解嘅原因。如果錄音為空、冇有效語音或者客戶冇講嘢，請直接喺摘要寫明冇識別到有效內容。

                分析報告格式：交談主題：xxx

                - 來電號碼：#{call_from}（必須記錄）

                - 內容摘要：xxx（如果錄音為空、冇有效語音或者客戶冇講嘢，請直接喺摘要寫明冇識別到有效內容）

                - 客人情感與情緒：xxx

                - 待辦：
                1. xxx
                2. xxx

                - 客人落單內容（如果冇則忽略，需要記錄唔同店鋪嘅訂單，請唔好包含規格大小，只需要數量同商品名稱）：
                1. One case of B/IN BREAST 帶骨雞胸
                2. Two boxes of LEG MT S/L 雞脾肉_無皮
                3. Three BEEF EYE ROUND 牛眼肉

                注意：客戶對一個或多個商品重複落單，請再次檢查並同客戶確認。（如果冇重複落單則忽略）

                客戶落單商品可參考但不限於以下列表：#{customer_items}
                """,
            _ =>
                """
                You are an analyst of telephone recordings. You make an accurate analysis of the content and tone of the recordings and write an analysis report.Even if the recording is not readable, please output a complete report and include in the summary an explanation of why the recording is missing or incomprehensible.If the recording is empty, or there is no valid voice or the customer did not speak, please directly write in the summary that no valid content was recognized

                Format of analysis report: Conversation topic: xxx

                - Coming number：#{call_from} （must be recorded）

                - Content summary: xxx（If the recording is empty, or there is no valid voice or the customer did not speak, please directly write in the summary that no valid content was recognized）

                - Guest emotions and mood: xxx

                - To-do:
                1. xxx
                2. xxx

                - Customer order content (ignore if none, Need to record orders in different stores, Please do not include the size!only the quantity and item name are required):
                1. One case of B/IN BREAST 帶骨雞胸
                2. Two boxes of LEG MT S/L 雞脾肉_無皮
                3. Three BEEF EYE ROUND 牛眼肉

                Note: The customer placed duplicate orders for one or more items. Please double-check and confirm with them. (ignore if none duplicate orders for one or more items.)

                The items ordered by customers may refer to the following list but are not limited to it.: #{customer_items}
                """
        };
    }

    private static string NormalizeLanguage(string modelLanguage)
    {
        var value = (modelLanguage ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(value)) return "en";

        if (value is "zh" or "cn" or "chinese" or "mandarin" || value.Contains("中文") || value.Contains("普通話"))
            return "zh";

        if (value is "cantonese" or "yue" or "hk" || value.Contains("廣東") || value.Contains("粵") || value.Contains("粤"))
            return "cantonese";

        return "en";
    }

    private static bool AssistantHasPhoneChannel(Domain.AISpeechAssistant.AiSpeechAssistant assistant)
    {
        if (string.IsNullOrWhiteSpace(assistant.Channel)) return false;

        return assistant.Channel
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Any(x =>
                x == ((int)AiSpeechAssistantChannel.PhoneChat).ToString() ||
                string.Equals(x, AiSpeechAssistantChannel.PhoneChat.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSharedCrmAutoSyncSalesAgent(Agent agent)
    {
        return agent.Type == AgentType.Sales &&
               agent.SourceSystem == AgentSourceSystem.AiResource;
    }

    private class KnowledgeCapabilityContext
    {
        public CompanyStore Store { get; set; }

        public bool CanConfigure { get; set; }

        public List<KnowledgeCapabilityRecord> Records { get; set; } = [];
    }

    private class KnowledgeCapabilityRecord
    {
        public Agent Agent { get; set; }

        public Domain.AISpeechAssistant.AiSpeechAssistant Assistant { get; set; }

        public AiSpeechAssistantKnowledge Knowledge { get; set; }
    }
}
