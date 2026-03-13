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
        await BuildAssistantDataAsync(cancellationToken).ConfigureAwait(false);

        return BuildSessionOptions();
    }
}
