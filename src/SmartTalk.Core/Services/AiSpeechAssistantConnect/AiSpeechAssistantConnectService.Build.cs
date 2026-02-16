using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private static AiSpeechAssistantConnectContext BuildContext(ConnectAiSpeechAssistantCommand command) => new()
    {
        Host = command.Host,
        From = command.From,
        To = command.To,
        AssistantId = command.AssistantId,
        NumberId = command.NumberId,
        TwilioWebSocket = command.TwilioWebSocket,
        OrderRecordType = command.OrderRecordType,
        LastUserInfo = new AiSpeechAssistantUserInfoDto { PhoneNumber = command.From }
    };

    private async Task<RealtimeSessionOptions> BuildSessionConfigAsync(CancellationToken cancellationToken)
    {
        await BuildKnowledgeAsync(cancellationToken).ConfigureAwait(false);

        _ctx.HumanContactPhone = (await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantHumanContactByAssistantIdAsync(_ctx.Assistant.Id, cancellationToken)
            .ConfigureAwait(false))?.HumanPhone;

        var modelConfig = await BuildModelConfigAsync(cancellationToken).ConfigureAwait(false);

        var timer = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantTimerByAssistantIdAsync(_ctx.Assistant.Id, cancellationToken).ConfigureAwait(false);

        return BuildSessionOptions(modelConfig, timer);
    }
}
