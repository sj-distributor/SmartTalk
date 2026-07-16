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

        EnsureCustomerItemsTool();
        ResolveCandidateCustomerIds();
    }

    private void EnsureCustomerItemsTool()
    {
        if (_ctx.Assistant.ModelProvider != RealtimeAiProvider.OpenAi) return;
        if (_ctx.FunctionCalls.Any(x => x.Type == AiSpeechAssistantSessionConfigType.Tool && x.Name == OpenAiToolConstants.QueryCustomerItemsByStoreName)) return;

        var content = new
        {
            type = "function",
            name = OpenAiToolConstants.QueryCustomerItemsByStoreName,
            description = "Use when a customer asks about HiFood warehouse product information, stock, availability, or orderable goods for a specific store. The customer will usually provide a store name, not a customer id. This tool searches CRM by store name, intersects CRM results with the current assistant customer ids, and returns at most 150 scoped item lines.",
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
