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
    }
}
