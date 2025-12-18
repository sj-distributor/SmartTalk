using Serilog;
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
            
            return;
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
    }
}