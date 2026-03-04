using Serilog;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Events.PhoneOrder;

namespace SmartTalk.Core.Services.EventHandling;

public partial class EventHandlingService
{
    public async Task HandlingEventAsync(PhoneOrderRecordUpdatedEvent @event, CancellationToken cancellationToken)
    {
        if (@event.OriginalScenarios == null || @event.DialogueScenarios == @event.OriginalScenarios) return;

        if (@event.OriginalScenarios == DialogueScenarios.Order && @event.DialogueScenarios != DialogueScenarios.Order)
        {
            var order = await _posDataProvider.GetPosOrderByIdAsync(recordId: @event.RecordId, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (order == null) return;
            
            await _posDataProvider.DeletePosOrdersAsync([order], cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        
        if (@event.OriginalScenarios != DialogueScenarios.Order && @event.DialogueScenarios == DialogueScenarios.Order)
        {
            var record = await _phoneOrderDataProvider.GetPhoneOrderRecordByIdAsync(@event.RecordId, cancellationToken).ConfigureAwait(false);

            if (record == null) return;
            
            var transcriptionText = record.TranscriptionText;

            if (string.IsNullOrWhiteSpace(transcriptionText)) return;
            
            var (aiSpeechAssistant, agent) = await _aiSpeechAssistantDataProvider.GetAgentAndAiSpeechAssistantAsync(record.AgentId, record.AssistantId, cancellationToken).ConfigureAwait(false); 
            
            Log.Information("Update Scenario Event: Assistant: {@Assistant} and Agent: {@Agent} by agent id {agentId}", aiSpeechAssistant, agent, record.AgentId);

            if (agent == null || aiSpeechAssistant == null) return;
            
            await _posUtilService.GenerateAiDraftAsync(agent, aiSpeechAssistant, record, cancellationToken).ConfigureAwait(false);
        }

        if (@event.OriginalScenarios != DialogueScenarios.InformationNotification && @event.OriginalScenarios != DialogueScenarios.ThirdPartyOrderNotification && @event.DialogueScenarios is DialogueScenarios.ThirdPartyOrderNotification or DialogueScenarios.InformationNotification)
            await RegenerateAiDraftAsync(@event.RecordId, cancellationToken).ConfigureAwait(false);
        
        if (@event.OriginalScenarios is not DialogueScenarios.Reservation && @event.DialogueScenarios is DialogueScenarios.Reservation)
            await RegenerateAiDraftAsync(@event.RecordId, cancellationToken).ConfigureAwait(false);

        if (@event.OriginalScenarios is DialogueScenarios.Reservation or DialogueScenarios.InformationNotification or DialogueScenarios.ThirdPartyOrderNotification or DialogueScenarios.Order)
        {
            var waitingEvent = (await _phoneOrderDataProvider.GetWaitingProcessingEventsAsync(recordId: @event.RecordId, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();

            if (waitingEvent == null) return;
            
            if (@event.DialogueScenarios is DialogueScenarios.Reservation or DialogueScenarios.InformationNotification or DialogueScenarios.ThirdPartyOrderNotification or DialogueScenarios.Order)
            {
                waitingEvent.TaskType = @event.DialogueScenarios switch
                {
                    DialogueScenarios.Order => TaskType.Order,
                    DialogueScenarios.Reservation or DialogueScenarios.InformationNotification or DialogueScenarios.ThirdPartyOrderNotification => TaskType.InformationNotification,
                    _ => throw new ArgumentOutOfRangeException()
                };
                
                await _phoneOrderDataProvider.UpdateWaitingProcessingEventsAsync([waitingEvent], cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
                await _phoneOrderDataProvider.DeleteWaitingProcessingEventAsync(waitingEvent, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        if (@event.OriginalScenarios != DialogueScenarios.Reservation && 
            @event.OriginalScenarios != DialogueScenarios.InformationNotification && 
            @event.OriginalScenarios != DialogueScenarios.ThirdPartyOrderNotification && 
            @event.OriginalScenarios != DialogueScenarios.Order && 
            @event.DialogueScenarios is DialogueScenarios.Order or 
                DialogueScenarios.InformationNotification or 
                DialogueScenarios.ThirdPartyOrderNotification or 
                DialogueScenarios.Reservation)
        {
            var waitingEvent = (await _phoneOrderDataProvider.GetWaitingProcessingEventsAsync(recordId: @event.RecordId, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();

            if (waitingEvent != null) return;

            var record = (await _phoneOrderDataProvider.GetPhoneOrderRecordAsync(recordId: @event.RecordId, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();

            if (record == null) return;
            
            var scenarioInformation = await _phoneOrderProcessJobService.IdentifyDialogueScenariosAsync(record.TranscriptionText, cancellationToken).ConfigureAwait(false);
            
            record.Remark = scenarioInformation.Remark;
            
            await _phoneOrderUtilService.GenerateWaitingProcessingEventAsync(record, scenarioInformation.IsIncludeTodo, record.AgentId, cancellationToken).ConfigureAwait(false);
        }
    }
    
    private async Task RegenerateAiDraftAsync(int recordId, CancellationToken cancellationToken)
    {
        var record = await _phoneOrderDataProvider.GetPhoneOrderRecordByIdAsync(recordId, cancellationToken).ConfigureAwait(false);

        var agent = await _agentDataProvider.GetAgentByIdAsync(record.AgentId, cancellationToken: cancellationToken).ConfigureAwait(false);
            
        var reservation = await _posDataProvider.GetPhoneOrderReservationInformationAsync(record.Id, cancellationToken).ConfigureAwait(false);
            
        if (reservation != null)
            await _posDataProvider.DeletePhoneOrderReservationInformationAsync(reservation, cancellationToken).ConfigureAwait(false);
            
        await _phoneOrderUtilService.GenerateAiDraftAsync(record, agent, cancellationToken).ConfigureAwait(false);
    }
}