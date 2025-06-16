using Serilog;
using Newtonsoft.Json;
using System.Globalization;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Messages.Commands.Pos;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Enums.Caching;
using SmartTalk.Messages.Enums.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Services.Pos;

public partial interface IPosService
{
    Task<GetPosStoreOrdersResponse> GetPosStoreOrdersAsync(GetPosStoreOrdersRequest request, CancellationToken cancellationToken = default);
    
    Task<PlacePosOrderResponse> PlacePosStoreOrdersAsync(PlacePosOrderCommand command, CancellationToken cancellationToken = default);
    
    Task UpdatePosOrderAsync(UpdatePosOrderCommand command, CancellationToken cancellationToken);
    
    Task<GetPosOrderProductsResponse> GetPosOrderProductsAsync(GetPosOrderProductsRequest request, CancellationToken cancellationToken);
}

public partial class PosService
{
    public async Task<GetPosStoreOrdersResponse> GetPosStoreOrdersAsync(GetPosStoreOrdersRequest request, CancellationToken cancellationToken = default)
    {
        var storeOrders = await _posDataProvider.GetPosOrdersAsync(
            request.StoreId, request.Keyword, request.StartDate, request.EndDate, cancellationToken).ConfigureAwait(false);

        return new GetPosStoreOrdersResponse
        {
            Data = _mapper.Map<List<PosOrderDto>>(storeOrders)
        };
    }

    public async Task<PlacePosOrderResponse> PlacePosStoreOrdersAsync(PlacePosOrderCommand command, CancellationToken cancellationToken = default)
    {
        var order = await _posDataProvider.GetPosOrderByIdAsync(orderId: command.OrderId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (order == null) throw new Exception("Order could not be found.");
        
        _mapper.Map(command, order);
        
        var token = await GetPosTokenAsync(order.StoreId, cancellationToken).ConfigureAwait(false);
        
        await SafetyPlaceOrderAsync(order, token, command.OrderItems, command.IsWithRetry, cancellationToken).ConfigureAwait(false);

        return new PlacePosOrderResponse
        {
            Data = _mapper.Map<PosOrderDto>(order)
        };
    }

    public async Task UpdatePosOrderAsync(UpdatePosOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await _posDataProvider.GetPosOrderByIdAsync(posOrderId: command.OrderId.ToString(), cancellationToken: cancellationToken).ConfigureAwait(false);

        if (order == null)
        {
            Log.Error("Order could not be found.");
            return;
        }
    
        var store = await _posDataProvider.GetPosCompanyStoreAsync(id: order.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (store == null || string.IsNullOrEmpty(store.AppId) || string.IsNullOrEmpty(store.AppSecret))
        {
            Log.Error("Store could not be found or app id could not be found.");
            return;
        }
    
        var response = await _easyPosClient.GetPosOrderAsync(new GetOrderRequestDto
        {
            AppId = store.AppId,
            AppSecret = store.AppSecret,
            OrderId = command.OrderId,
        }, cancellationToken).ConfigureAwait(false);

        if (response?.Data.Order == null || response.Success == false)
            throw new Exception($"Order {command.OrderId} could not be found.");

        var address = response.Data.Order.Customer.Addresses.FirstOrDefault();

        order.Notes = response.Data.Order.Notes;
        order.Name = response.Data.Order.Customer.Name;
        order.Phone = response.Data.Order.Customer.Phone;
        order.Address = string.IsNullOrEmpty(address?.FullAddress) ? order.Address : address.FullAddress;
        order.Latitude = address?.Lat.ToString(CultureInfo.InvariantCulture);
        order.Longitude = address?.Lng.ToString(CultureInfo.InvariantCulture);
        order.Room = string.IsNullOrEmpty(address?.Room) ? string.Empty : address.Room;

        var itemStatus = BuildPosOrderItemStatus(response.Data.Order.OrderItems, order.Items);
        
        var items = response.Data.Order.OrderItems.Select(item => new PosOrderItemDto
        {
            Id = item.Id,
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            OriginalPrice = item.OriginalPrice,
            Price = item.Price,
            Notes = string.IsNullOrEmpty(item.Notes) ? string.Empty : item.Notes,
            OrderItemModifiers = _mapper.Map<List<PhoneCallOrderItemModifiers>>(item.OrderItemModifiers),
            Status = itemStatus.Where(kv => kv.Value.Contains(item.ProductId)).Select(kv => (PosOrderItemStatus?)kv.Key).FirstOrDefault()
        }).ToList();
        
        var itemJson = JsonConvert.SerializeObject(items);
        order.Items = itemJson;
        
        await _posDataProvider.UpdatePosOrdersAsync([order], cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<GetPosOrderProductsResponse> GetPosOrderProductsAsync(GetPosOrderProductsRequest request, CancellationToken cancellationToken)
    {
        var products = await _posDataProvider.GetPosProductsAsync(
            storeId: request.StoreId, productIds: request.ProductIds, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var menuWithCategories = await _posDataProvider.GetPosMenuInfosAsync(request.StoreId, products.Select(x => x.CategoryId).ToList(), cancellationToken).ConfigureAwait(false);
        
        return new GetPosOrderProductsResponse
        {
            Data = BuildPosOrderProductsData(products, menuWithCategories)
        };
    }

    private List<GetPosOrderProductsResponseData> BuildPosOrderProductsData(List<PosProduct> products, List<(PosMenu Menu, PosCategory Category)> menuWithCategories)
    {
        return products.Select(product =>
        {
            var result = menuWithCategories.Where(x => x.Category.Id == product.CategoryId).FirstOrDefault();
            
            return new GetPosOrderProductsResponseData
            {
                Menu = result.Menu == null ? null : _mapper.Map<PosMenuDto>(result.Menu),
                Category = result.Menu == null ? null : _mapper.Map<PosCategoryDto>(result.Category),
                Product = result.Menu == null ? null : _mapper.Map<PosProductDto>(product),
            };
        }).ToList();
    }

    private Dictionary<PosOrderItemStatus, List<long>> BuildPosOrderItemStatus(List<EasyPosOrderItemDto> orderItems, string originalItemJson)
    {
        var originalItems = JsonConvert.DeserializeObject<List<PhoneCallOrderItem>>(originalItemJson);

        var newProductIds = orderItems.Select(o => o.ProductId).ToHashSet();
        var oldProductIds = originalItems.Select(o => o.ProductId).ToHashSet();

        var added = newProductIds.Except(oldProductIds).ToList();
        var missed = oldProductIds.Except(newProductIds).ToList();
        var normal = newProductIds.Intersect(oldProductIds).ToList();

        return new Dictionary<PosOrderItemStatus, List<long>>
        {
            [PosOrderItemStatus.Added] = added,
            [PosOrderItemStatus.Missed] = missed,
            [PosOrderItemStatus.Normal] = normal
        };
    }

    private async Task<string> GetPosTokenAsync(int storeId, CancellationToken cancellationToken)
    {
        var store = await _posDataProvider.GetPosCompanyStoreAsync(id: storeId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (store == null) throw new Exception("Store could not be found.");
        
        var authorization = await _easyPosClient.GetEasyPosTokenAsync(new EasyPosTokenRequestDto
        {
            AppId = store.AppId,
            AppSecret = store.AppSecret
        }, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Getting the store pos token");

        if (authorization == null || string.IsNullOrEmpty(authorization.Data) || !authorization.Success)
        {
            throw new Exception("Failed to get token");
        }

        return authorization.Data;
    }

    private async Task<bool> ValidatePosProductsAsync(PosOrder order, string token, CancellationToken cancellationToken)
    {
        try
        {
            var items = JsonConvert.DeserializeObject<List<PosOrderItemDto>>(order.Items);

            var response = await _easyPosClient.ValidatePosProductsAsync(new ValidatePosProductRequestDto
            {
                ProductIds = items.Select(x => x.ProductId).ToList(),
            }, token, cancellationToken).ConfigureAwait(false);
        
            Log.Information("Validating pos products response: {@Response}", response);

            if (response?.Data == null)
            {
                Log.Error("Failed to get pos products");
                return false;
            }

            if (response.Data.Count == 0) return true;
        
            foreach (var item in items)
            {
                var result = response.Data.Any(x => x == item.ProductId);

                if (!result) continue;

                item.Status = PosOrderItemStatus.Missed;
            }
        
            order.Items = JsonConvert.SerializeObject(items);
        
            return false;
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to validate pos products: {e.Message}");
        }
    }

    private async Task SafetyPlaceOrderAsync(PosOrder order, string token, string orderItems, bool isWithRetry, CancellationToken cancellationToken)
    {
        var lockKey = $"place-order-key-{order.Id}";
        await _redisSafeRunner.ExecuteWithLockAsync(lockKey, async () =>
        {
            if(order.Status == PosOrderStatus.Sent) throw new Exception("Order is already sent.");

            var result = await ValidatePosProductsAsync(order, token, cancellationToken).ConfigureAwait(false);

            if (!result)
            {
                await MarkOrderAsSpecificStatusAsync(order, PosOrderStatus.Modified, cancellationToken).ConfigureAwait(false);
                return;
            }
        
            order.Items = orderItems;

            var orderId = isWithRetry
                ? await SafetyPlaceOrderWithRetryIfRequiredAsync(order, token, cancellationToken).ConfigureAwait(false)
                : await SafetyPlaceOrderIfRequiredAsync(order, token, cancellationToken).ConfigureAwait(false);
        
            order.OrderId = orderId;
            
            await _posDataProvider.UpdatePosOrdersAsync([order], cancellationToken: cancellationToken).ConfigureAwait(false);
        }, wait: TimeSpan.FromSeconds(10), retry: TimeSpan.FromSeconds(1), server: RedisServer.System).ConfigureAwait(false);
    }

    private async Task<string> SafetyPlaceOrderWithRetryIfRequiredAsync(PosOrder order, string token, CancellationToken cancellationToken)
    {
        const int maxRetries = 4;
        var orderId = string.Empty;
        
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await _easyPosClient.PlaceOrderAsync(new PlaceOrderToEasyPosRequestDto
                {
                    Type = order.Type == PosOrderReceiveType.Pickup ? 1 : 3,
                    IsTaxFree = false,
                    Notes = order.Notes,
                    SourceType = 3,
                    OrderItems = JsonConvert.DeserializeObject<List<PhoneCallOrderItem>>(order.Items),
                    Customer = new PhoneCallOrderCustomer
                    {
                        Name = order.Name,
                        Phone = order.Phone,
                        Addresses =
                        [
                            new PhoneCallOrderCustomerAddress
                            {
                                FullAddress = string.IsNullOrEmpty(order.Address) ? string.Empty : order.Address,
                                Room = string.IsNullOrEmpty(order.Room) ? string.Empty : order.Room,
                                AddressImg = string.Empty,
                                City = string.Empty,
                                State = string.Empty,
                                PostalCode = string.Empty,
                                Country = string.Empty,
                                Line1 = string.Empty,
                                Line2 = string.Empty,
                                Lat = string.IsNullOrEmpty(order.Latitude) ? 0 : double.Parse(order.Latitude, CultureInfo.InvariantCulture),
                                Lng = string.IsNullOrEmpty(order.Longitude) ? 0 : double.Parse(order.Longitude, CultureInfo.InvariantCulture)
                            }
                        ]
                    }
                }, token, attempt == 1 ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);

                Log.Information("Place order: {@Order} at attempt {attempt} and response is: {@Response}", order, attempt, response);

                if (response is { Success: true })
                {
                    await MarkOrderAsSpecificStatusAsync(order, PosOrderStatus.Sent, cancellationToken).ConfigureAwait(false);

                    orderId = response.Data?.Order?.Id.ToString() ?? string.Empty;
                    
                    break;
                }

                if (order.Status != PosOrderStatus.Error)
                    await MarkOrderAsSpecificStatusAsync(order, PosOrderStatus.Error, cancellationToken).ConfigureAwait(false);

                if (attempt < maxRetries)
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
            catch (Exception ex)
            {
                if (order.Status != PosOrderStatus.Error)
                    await MarkOrderAsSpecificStatusAsync(order, PosOrderStatus.Error, cancellationToken).ConfigureAwait(false);

                if (attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                    continue;
                }

                Log.Information("Place order {@Order}: All auto retry failed", order);
            }
        }
        
        return orderId;
    }

    private async Task<string> SafetyPlaceOrderIfRequiredAsync(PosOrder order, string token, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _easyPosClient.PlaceOrderAsync(new PlaceOrderToEasyPosRequestDto
            {
                Type = 1,
                IsTaxFree = false,
                Notes = string.IsNullOrEmpty(order.Notes) ? string.Empty : order.Notes,
                OrderItems = JsonConvert.DeserializeObject<List<PhoneCallOrderItem>>(order.Items),
                Customer = new PhoneCallOrderCustomer
                {
                    Name = order.Name,
                    Phone = order.Phone,
                    Addresses =
                    [
                        new PhoneCallOrderCustomerAddress
                        {
                            FullAddress = string.IsNullOrEmpty(order.Address) ? string.Empty : order.Address,
                            Room = string.IsNullOrEmpty(order.Room) ? string.Empty : order.Room,
                            AddressImg = string.Empty,
                            City = string.Empty,
                            State = string.Empty,
                            PostalCode = string.Empty,
                            Country = string.Empty,
                            Line1 = string.Empty,
                            Line2 = string.Empty,
                            Lat = string.IsNullOrEmpty(order.Latitude) ? 0 : double.Parse(order.Latitude, CultureInfo.InvariantCulture),
                            Lng = string.IsNullOrEmpty(order.Longitude) ? 0 : double.Parse(order.Longitude, CultureInfo.InvariantCulture)
                        }
                    ]
                }
            }, token, TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);

            Log.Information("Place order: {@Order} and response is: {@Response}", order, response);

            if (response is { Success: true })
            {
                await MarkOrderAsSpecificStatusAsync(order, PosOrderStatus.Sent, cancellationToken).ConfigureAwait(false);
                
                return response.Data?.Order?.Id.ToString() ?? string.Empty;
            }

            if (order.Status != PosOrderStatus.Error)
                await MarkOrderAsSpecificStatusAsync(order, PosOrderStatus.Error, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (order.Status != PosOrderStatus.Error)
                await MarkOrderAsSpecificStatusAsync(order, PosOrderStatus.Error, cancellationToken).ConfigureAwait(false);

            Log.Information("Place order {@Order} failed: {@Message}", order, ex.Message);
        }
        
        return string.Empty;
    }

    private async Task MarkOrderAsSpecificStatusAsync(PosOrder order, PosOrderStatus status, CancellationToken cancellationToken)
    {
        order.Status = status;
        
        await _posDataProvider.UpdatePosOrdersAsync([order], cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}