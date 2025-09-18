using Newtonsoft.Json.Linq;
using Serilog;
using Smarties.Messages.DTO.OpenAi;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Messages.Enums.Pos;
using SmartTalk.Messages.Events.AiSpeechAssistant;
using SmartTalk.Messages.Events.Pos;

namespace SmartTalk.Core.Services.EventHandling;

public partial class EventHandlingService
{
    public async Task HandlingEventAsync(PosOrderPlacedEvent @event, CancellationToken cancellationToken)
    {
        if (@event == null || string.IsNullOrEmpty(@event.Order?.Phone)) return;
        
        var customer = await _posDataProvider.GetStoreCustomerAsync(phone: @event.Order.Phone, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (customer == null)
        {
            customer = new StoreCustomer
            {
                StoreId = @event.Order.StoreId,
                Phone = @event.Order.Phone,
                Name = @event.Order.Name,
                Address = @event.Order.Address,
                Latitude = @event.Order.Latitude,
                Longitude = @event.Order.Longitude,
                Remarks = @event.Order.Notes
            };
            
            await _posDataProvider.AddStoreCustomersAsync([customer], cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (!string.IsNullOrEmpty(@event.Order.Name) && @event.Order.Name.Trim() != customer.Name.Trim())
                customer.Name = @event.Order.Name;

            if (@event.Order.Type == PosOrderReceiveType.Delivery)
            {
                if (!string.IsNullOrEmpty(@event.Order.Address) && @event.Order.Address?.Trim() != customer.Address?.Trim())
                    customer.Address = @event.Order.Address;
            
                if (!string.IsNullOrEmpty(@event.Order.Latitude) && @event.Order.Latitude?.Trim() != customer.Latitude?.Trim())
                    customer.Latitude = @event.Order.Latitude;
            
                if (!string.IsNullOrEmpty(@event.Order.Longitude) && @event.Order.Longitude?.Trim() != customer.Longitude?.Trim())
                    customer.Longitude = @event.Order.Longitude;
            
                if (@event.Order.Room?.Trim() != customer.Room?.Trim())
                    customer.Room = @event.Order.Room;
            
                if (@event.Order.Remarks?.Trim() != customer.Remarks?.Trim())
                    customer.Remarks = @event.Order.Remarks;
            }
            
            await _posDataProvider.UpdateStoreCustomersAsync([customer], cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}