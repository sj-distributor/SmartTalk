using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService
{
    Task ConfigureAiSpeechAssistantInboundRouteAsync(ConfigureAiSpeechAssistantInboundRouteCommand command, CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantService
{
    public async Task ConfigureAiSpeechAssistantInboundRouteAsync(ConfigureAiSpeechAssistantInboundRouteCommand command, CancellationToken cancellationToken)
    {
        var inboundRoutes = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantInboundRouteByTargetNumberAsync(command.TargetNumber, cancellationToken).ConfigureAwait(false);

        if (inboundRoutes.Count == 0) return;

        await _aiSpeechAssistantDataProvider.DeleteAiSpeechAssistantInboundRoutesAsync(inboundRoutes, true, cancellationToken).ConfigureAwait(false);
        
        if (command.Rollback) return;

        await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantInboundRoutesAsync([
            new AiSpeechAssistantInboundRoute
            {
                To = command.TargetNumber,
                ForwardNumber = command.ForwardNUmber,
                TimeZone = "Asia/Shanghai",
                DayOfWeek = "0,1,2,3,4,5,6",
                IsFullDay = true
            }
        ], true, cancellationToken).ConfigureAwait(false);
    }
}