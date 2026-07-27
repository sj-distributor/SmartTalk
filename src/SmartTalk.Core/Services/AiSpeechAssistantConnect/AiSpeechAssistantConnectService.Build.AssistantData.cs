using Newtonsoft.Json;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private async Task BuildAssistantDataAsync(CancellationToken cancellationToken)
    {
        var assistantId = _ctx.Assistant.Id;

        _ctx.HumanContactPhone = (await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantHumanContactByAssistantIdAsync(assistantId, cancellationToken).ConfigureAwait(false))?.HumanPhone;

        _ctx.Timer = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantTimerByAssistantIdAsync(assistantId, cancellationToken).ConfigureAwait(false);

        _ctx.FunctionCalls = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantFunctionCallByAssistantIdsAsync([assistantId], _ctx.Assistant.ModelProvider, true, cancellationToken).ConfigureAwait(false);

        ResolveCandidateCustomerIds();
        EnsureCustomerItemsTool();
    }

    private void EnsureCustomerItemsTool()
    {
        if (_ctx.Assistant.ModelProvider != RealtimeAiProvider.OpenAi) return;
        if (_ctx.CandidateCustomerIds.Count <= 1 || string.IsNullOrWhiteSpace(_ctx.CustomerItemsPromptTemplate))
        {
            _ctx.FunctionCalls.RemoveAll(x =>
                x.Type == AiSpeechAssistantSessionConfigType.Tool &&
                x.Name == OpenAiToolConstants.QueryCustomerItemsByStoreName);
            return;
        }

        if (_ctx.FunctionCalls.Any(x => x.Type == AiSpeechAssistantSessionConfigType.Tool && x.Name == OpenAiToolConstants.QueryCustomerItemsByStoreName)) return;

        var content = new
        {
            type = "function",
            name = OpenAiToolConstants.QueryCustomerItemsByStoreName,
            description = "Confirm a store name in a multi-store call and load that store's cached HiFood customer items into the current session knowledge.",
            parameters = new
            {
                type = "object",
                properties = new
                {
                    store_name = new
                    {
                        type = "string",
                        description = "The store or restaurant name mentioned by the customer."
                    },
                    product_name = new
                    {
                        type = "string",
                        description = "Optional product standard name or customer nickname mentioned by the customer."
                    },
                    prefetch_only = new
                    {
                        type = "boolean",
                        description = "Set true only when the guest merely provides or corrects the store name and is not asking a product, stock, availability, warehouse, or orderable-goods question. The matching customer item cache replaces the session knowledge placeholder silently for a later guest question."
                    }
                },
                required = new[] { "store_name" }
            }
        };

        _ctx.FunctionCalls.Add(new AiSpeechAssistantFunctionCall
        {
            AssistantId = _ctx.Assistant.Id,
            Name = OpenAiToolConstants.QueryCustomerItemsByStoreName,
            Content = JsonConvert.SerializeObject(content),
            Type = AiSpeechAssistantSessionConfigType.Tool,
            ModelProvider = RealtimeAiProvider.OpenAi,
            IsActive = true
        });
    }

    private void ResolveCandidateCustomerIds()
    {
        _ctx.CandidateCustomerIds = SplitAssistantCustomerIds(_ctx.Assistant?.Name);
    }
}
