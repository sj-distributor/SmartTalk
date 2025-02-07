using Newtonsoft.Json;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderService
{
    Task<GetPhoneOrderOrderItemsRessponse> GetPhoneOrderOrderItemsAsync(GetPhoneOrderOrderItemsRequest request, CancellationToken cancellationToken);

    Task<PlaceOrderAndModifyItemResponse> PlaceOrderAndModifyItemsAsync(PlaceOrderAndModifyItemCommand command, CancellationToken cancellationToken);
}

public partial class PhoneOrderService
{
    public async Task<GetPhoneOrderOrderItemsRessponse> GetPhoneOrderOrderItemsAsync(GetPhoneOrderOrderItemsRequest request, CancellationToken cancellationToken)
    {
        var orderItems = await _phoneOrderDataProvider.GetPhoneOrderOrderItemsAsync(request.RecordId, cancellationToken: cancellationToken).ConfigureAwait(false);

        var record = (await _phoneOrderDataProvider.GetPhoneOrderRecordAsync(request.RecordId, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        
        return new GetPhoneOrderOrderItemsRessponse
        {
            Data = new GetPhoneOrderOrderItemsData
            {
                ManualOrderId = record.ManualOrderId.ToString(),
                ManualItems = _mapper.Map<List<PhoneOrderOrderItemDto>>(orderItems.Where(x => x.OrderType == PhoneOrderOrderType.ManualOrder).ToList()),
                AIItems = _mapper.Map<List<PhoneOrderOrderItemDto>>(orderItems.Where(x => x.OrderType == PhoneOrderOrderType.AIOrder).ToList())
            }
        };
    }

    public async Task<PlaceOrderAndModifyItemResponse> PlaceOrderAndModifyItemsAsync(PlaceOrderAndModifyItemCommand command, CancellationToken cancellationToken)
    {
        var items = await _phoneOrderDataProvider
            .GetPhoneOrderOrderItemsAsync(command.RecordId, PhoneOrderOrderType.AIOrder, cancellationToken).ConfigureAwait(false);

        await _phoneOrderDataProvider.DeletePhoneOrderItemsAsync(items, cancellationToken: cancellationToken).ConfigureAwait(false);

        var orderItems = await _phoneOrderDataProvider.AddPhoneOrderItemAsync(_mapper.Map<List<PhoneOrderOrderItem>>(command.OrderItems), cancellationToken: cancellationToken).ConfigureAwait(false);

        var menuItems = await _restaurantDataProvider.GetRestaurantMenuItemsAsync(
            ids: orderItems.Where(x => x.MenuItemId.HasValue).Select(x => x.MenuItemId.Value).ToList(), cancellationToken: cancellationToken).ConfigureAwait(false);

        var response = await _easyPosClient.PlaceOrderToEasyPosAsync(new PlaceOrderToEasyPosRequestDto
        {
            Type = 9,
            IsTaxFree = true,
            Notes = string.Empty,
            OrderItems = orderItems.Select(x => new PhoneCallOrderItem
            {
                ProductId = menuItems.FirstOrDefault(m => m.Id == x.MenuItemId)?.ProductId ?? 0,
                Quantity = x.Quantity,
                OriginalPrice = x.Price,
                Price = x.Price,
                Notes = x.Note,
                OrderItemModifiers = JsonConvert.DeserializeObject<List<PhoneCallOrderItemModifiers>>(menuItems.FirstOrDefault(m => m.Id == x.MenuItemId)?.OrderItemModifiers ?? string.Empty)
            }).ToList()
        }, cancellationToken).ConfigureAwait(false);

        return new PlaceOrderAndModifyItemResponse
        {
            Data = new PlaceOrderAndModifyItemResponseData
            {
                OrderNumber = response.Data.Order.OrderItems.First().OrderId.ToString(),
                OrderItems = _mapper.Map<List<PhoneOrderOrderItemDto>>(orderItems)
            }
        };
    }
}