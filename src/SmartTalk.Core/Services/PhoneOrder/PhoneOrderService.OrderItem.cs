using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Domain.Restaurants;
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
                CustomerName = record?.CustomerName,
                OrderPhoneNumber = record?.PhoneNumber,
                ManualOrderId = record?.ManualOrderId.ToString(),
                ManualItems = _mapper.Map<List<PhoneOrderOrderItemDto>>(orderItems.Where(x => x.OrderType == PhoneOrderOrderType.ManualOrder).ToList()),
                AIItems = _mapper.Map<List<PhoneOrderOrderItemDto>>(orderItems.Where(x => x.OrderType == PhoneOrderOrderType.AIOrder).ToList())
            }
        };
    }

    public async Task<PlaceOrderAndModifyItemResponse> PlaceOrderAndModifyItemsAsync(PlaceOrderAndModifyItemCommand command, CancellationToken cancellationToken)
    {
        var record = await _phoneOrderDataProvider.GetPhoneOrderRecordByIdAsync(command.RecordId, cancellationToken).ConfigureAwait(false);
        
        var items = await _phoneOrderDataProvider
            .GetPhoneOrderOrderItemsAsync(command.RecordId, PhoneOrderOrderType.AIOrder, cancellationToken).ConfigureAwait(false);

        await _phoneOrderDataProvider.DeletePhoneOrderItemsAsync(items, cancellationToken: cancellationToken).ConfigureAwait(false);

        var orderItems = await _phoneOrderDataProvider.AddPhoneOrderItemAsync(_mapper.Map<List<PhoneOrderOrderItem>>(command.OrderItems), cancellationToken: cancellationToken).ConfigureAwait(false);

        Log.Information("Add new order items: {@OrderItems}", orderItems);
        
        var menuItems = await _restaurantDataProvider.GetRestaurantMenuItemsAsync(
            productIds: orderItems.Where(x => x.ProductId.HasValue).Select(x => x.ProductId.Value).ToList(), cancellationToken: cancellationToken).ConfigureAwait(false);

        var request = new PlaceOrderToEasyPosRequestDto
        {
            Type = 1,
            IsTaxFree = true,
            Notes = string.Empty,
            OrderItems = orderItems.Select(x => new PhoneCallOrderItem
            {
                ProductId = x.ProductId ?? GetMenuItemByName(menuItems, x.FoodName)?.ProductId ?? 0,
                Quantity = x.Quantity,
                OriginalPrice = x.Price,
                Price = x.Price,
                Notes = string.IsNullOrEmpty(x.Note) ? string.Empty : x.Note,
                OrderItemModifiers = HandleSpecialMenuItems(menuItems, x)
            }).Where(x => x.ProductId != 0).ToList(),
            Customer = GetOrderCustomerInfo(record)
        };
        
        Log.Information("Generate easy pos order request: {@Request}", request);
        
        var response = await _easyPosClient.PlaceOrderToEasyPosAsync(request, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Place order response: {@Response}", response);

        var record = await _phoneOrderDataProvider.GetPhoneOrderRecordByIdAsync(command.RecordId, cancellationToken).ConfigureAwait(false);

        record.CustomerName = command.CustomerName;
        record.PhoneNumber = command.OrderPhoneNumber;
        
        if (response.Data == null || !response.Success)
        {
            await MarkPhoneOrderStatusAsSpecificAsync(record, PhoneOrderOrderStatus.Failed, cancellationToken).ConfigureAwait(false);
            
            throw new Exception("Can not place an order.");
        }

        await MarkPhoneOrderStatusAsSpecificAsync(record, PhoneOrderOrderStatus.Success, cancellationToken).ConfigureAwait(false);

        return new PlaceOrderAndModifyItemResponse
        {
            Data = new PlaceOrderAndModifyItemResponseData
            {
                OrderNumber = response.Data.Order.OrderItems.FirstOrDefault() != null ? response.Data.Order.OrderItems.First().OrderId.ToString() : string.Empty,
                OrderItems = _mapper.Map<List<PhoneOrderOrderItemDto>>(orderItems)
            }
        };
    }

    private RestaurantMenuItem GetMenuItemByName(List<RestaurantMenuItem> menuItems, string foodName)
    {
        return menuItems.FirstOrDefault(x => x.Name == foodName || x.Name.Contains(foodName));
    }

    private List<PhoneCallOrderItemModifiers> HandleSpecialMenuItems(List<RestaurantMenuItem> menuItems, PhoneOrderOrderItem orderItem)
    {
        var specialItems = menuItems.Where(x => x.ProductId.HasValue && x.ProductId == orderItem.ProductId).ToList();

        if (specialItems.Count == 0)
        {
            var item = GetMenuItemByName(menuItems, orderItem.FoodName);

            if (item == null || string.IsNullOrEmpty(item.OrderItemModifiers)) return [];

            return JsonConvert.DeserializeObject<List<PhoneCallOrderItemModifiers>>(item.OrderItemModifiers);
        }

        if (specialItems.Count == 1) return [];
        
        var specificationItem = GetMenuItemByName(specialItems, orderItem.FoodName);

        return JsonConvert.DeserializeObject<List<PhoneCallOrderItemModifiers>>(specificationItem?.OrderItemModifiers ?? string.Empty) ?? [];
    }

    private PhoneCallOrderCustomer GetOrderCustomerInfo(PhoneOrderRecord record)
    {
        if (record == null || string.IsNullOrEmpty(record.PhoneNumber)) return null;
            
        return new PhoneCallOrderCustomer
        {
            Name = record.CustomerName,
            Phone = record.PhoneNumber
        };
    }

    private async Task MarkPhoneOrderStatusAsSpecificAsync(PhoneOrderRecord record, PhoneOrderOrderStatus status, CancellationToken cancellationToken)
    {
        record.OrderStatus = status;

        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}