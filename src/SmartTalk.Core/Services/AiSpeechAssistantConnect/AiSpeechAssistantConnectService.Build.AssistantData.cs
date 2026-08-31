using Newtonsoft.Json;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.Core.Services.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private async Task BuildAssistantDataAsync(CancellationToken cancellationToken)
    {
        var assistantId = _ctx.Assistant.Id;

        _ctx.Timer = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantTimerByAssistantIdAsync(assistantId, cancellationToken).ConfigureAwait(false);

        _ctx.FunctionCalls = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantFunctionCallByAssistantIdsAsync([assistantId], _ctx.Assistant.ModelProvider, true, cancellationToken).ConfigureAwait(false);
        
        ResolveCandidateCustomerIds();
        EnsureCustomerItemsTool();
        AppendComplaintPromptInstructionIfRequired();
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

    private void AppendComplaintPromptInstructionIfRequired()
    {
        _ctx.Prompt = AiSpeechAssistantComplaintInfoHelper.AppendPromptInstructionIfEnabled(
            _ctx.Prompt,
            _ctx.FunctionCalls.Select(x => x.Name));
    }
}
