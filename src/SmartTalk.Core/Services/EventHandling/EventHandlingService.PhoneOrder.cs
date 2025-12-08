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
            
            var globalText = record.ConversationText;

            if (string.IsNullOrWhiteSpace(globalText)) return;

            await _phoneOrderUtilService.ExtractPhoneOrderShoppingCartAsync(globalText, record, cancellationToken).ConfigureAwait(false);
        }
    }
}