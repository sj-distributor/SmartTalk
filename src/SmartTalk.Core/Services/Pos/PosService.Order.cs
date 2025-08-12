using Serilog;
using Newtonsoft.Json;
using System.Globalization;
using SmartTalk.Core.Constants;
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
    
    Task<GetPosStoreOrderResponse> GetPosStoreOrderAsync(GetPosStoreOrderRequest request, CancellationToken cancellationToken);
    
    Task<GetPosCustomerInfoResponse> GetPosCustomerInfosAsync(GetPosCustomerInfoRequest request, CancellationToken cancellationToken);
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
        var order = await GetOrAddPosOrderAsync(command, cancellationToken).ConfigureAwait(false);
        
        var store = await _posDataProvider.GetPosCompanyStoreAsync(id: order.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (store == null) throw new Exception("Store could not be found.");

        var token = await GetPosTokenAsync(store, order, cancellationToken).ConfigureAwait(false);

        if (!store.IsLink && string.IsNullOrWhiteSpace(token))
        {
            order.Status = PosOrderStatus.Modified;
            
            await _posDataProvider.UpdatePosOrdersAsync([order], cancellationToken: cancellationToken).ConfigureAwait(false);
            
            return new PlacePosOrderResponse { Data = _mapper.Map<PosOrderDto>(order) };
        }
        
        await SafetyPlaceOrderAsync(order, store, token, command.IsWithRetry, 0, cancellationToken).ConfigureAwait(false);

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

        if (store == null || string.IsNullOrEmpty(store.Link) || string.IsNullOrEmpty(store.AppId) || string.IsNullOrEmpty(store.AppSecret))
        {
            Log.Error("Store could not be found or appId、url、secret could not be empty.");
            return;
        }
    
        var response = await _easyPosClient.GetPosOrderAsync(new GetOrderRequestDto
        {
            BaseUrl = store.Link,
            AppId = store.AppId,
            AppSecret = store.AppSecret,
            OrderId = command.OrderId,
        }, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get pos order response: {@Response}", response);

        if (response?.Data?.Order == null || response.Success == false)
            throw new Exception($"Order {command.OrderId} could not be found.");

        var address = response.Data.Order.Customer.Addresses.FirstOrDefault();
        var modifiedStatus = PosOrderModifiedStatusMapping(response.Data.Order.Status);

        order.Notes = response.Data.Order.Notes;
        order.Name = response.Data.Order.Customer.Name;
        order.Phone = response.Data.Order.Customer.Phone;
        order.Address = string.IsNullOrEmpty(address?.FullAddress) ? order.Address : address.FullAddress;
        order.Latitude = address?.Lat.ToString(CultureInfo.InvariantCulture);
        order.Longitude = address?.Lng.ToString(CultureInfo.InvariantCulture);
        order.Room = string.IsNullOrEmpty(address?.Room) ? string.Empty : address.Room;
        order.Type = response.Data.Order.Type == 1 ? PosOrderReceiveType.Pickup : PosOrderReceiveType.Delivery;
        order.ModifiedStatus = modifiedStatus;

        var items = BuildMergedOrderItemsWithStatus(response.Data.Order.OrderItems, order.Items);
        order.ModifiedItems = JsonConvert.SerializeObject(items);
        
        if (modifiedStatus == PosOrderModifiedStatus.Cancelled)
            order.Status = PosOrderStatus.Modified;
        else
        {
            if(response.Data.Order.TotalAmount == order.Total && items.All(x => x.Status == PosOrderItemStatus.Normal))
                order.Status = PosOrderStatus.Sent;
            else
                order.Status = PosOrderStatus.Modified;
        }
        
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

    public async Task<GetPosStoreOrderResponse> GetPosStoreOrderAsync(GetPosStoreOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await _posDataProvider.GetPosOrderByIdAsync(orderId: request.OrderId, recordId: request.RecordId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (order == null)
            throw new Exception($"Order could not be found by orderId: {request.OrderId} or recordId: {request.RecordId}.");

        return new GetPosStoreOrderResponse
        {
            Data = _mapper.Map<PosOrderDto>(order)
        };
    }

    public async Task<GetPosCustomerInfoResponse> GetPosCustomerInfosAsync(GetPosCustomerInfoRequest request, CancellationToken cancellationToken)
    {
        var customerInfos = await _posDataProvider.GetPosCustomerInfosAsync(request.Phone, cancellationToken).ConfigureAwait(false);

        return new GetPosCustomerInfoResponse { Data = customerInfos };
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
                Product = result.Menu == null ? null : _mapper.Map<PosProductDto>(product)
            };
        }).ToList();
    }

    private List<PosOrderItemDto> BuildMergedOrderItemsWithStatus(List<EasyPosOrderItemDto> newItems, string originalItemJson)
    {
        var result = new List<PosOrderItemDto>();
        var originalItems = JsonConvert.DeserializeObject<List<PosOrderItemDto>>(originalItemJson) ?? [];

        var newGroups = newItems.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.ToList());
        var originalGroups = originalItems.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.ToList());

        var allProductIds = originalGroups.Keys.Union(newGroups.Keys);

        foreach (var productId in allProductIds)
        {
            Log.Information("Current productId: {ProductId}", productId);
            
            var newList = newGroups.GetValueOrDefault(productId) ?? [];
            var originalList = originalGroups.GetValueOrDefault(productId) ?? [];
            
            Log.Information("Original item list: {@OriginalList}, New item list: {@NewList}", originalList, newList);

            if (newList.Count == originalList.Count)
            {
                Log.Information("Original items and new items have same count: {ProductId}", productId);
                
                result.AddRange(newList.Select(x => new PosOrderItemDto
                {
                    Id = x.Id,
                    ProductId = x.ProductId,
                    Quantity = x.Quantity,
                    OriginalPrice = x.OriginalPrice,
                    Price = x.Price,
                    Notes = x.Notes ?? string.Empty,
                    OrderItemModifiers = _mapper.Map<List<PhoneCallOrderItemModifiers>>(x.OrderItemModifiers),
                    Status = x.Quantity > 0 ? PosOrderItemStatus.Normal : PosOrderItemStatus.Missed
                }).ToList());
            }
            else
            {
                if (newList.Count == 0 && originalList.Count != 0)
                {
                    result.AddRange(originalList.Select(x => new PosOrderItemDto
                    {
                        Id = x.Id,
                        ProductId = x.ProductId,
                        Quantity = x.Quantity,
                        OriginalPrice = x.OriginalPrice,
                        Price = x.Price,
                        Notes = x.Notes ?? string.Empty,
                        OrderItemModifiers = _mapper.Map<List<PhoneCallOrderItemModifiers>>(x.OrderItemModifiers),
                        Status = PosOrderItemStatus.Missed
                    }).ToList());
                    
                    continue;
                }

                if (originalList.Count == 0 && newList.Count != 0)
                {
                    result.AddRange(newList.Select(x => new PosOrderItemDto
                    {
                        Id = x.Id,
                        ProductId = x.ProductId,
                        Quantity = x.Quantity,
                        OriginalPrice = x.OriginalPrice,
                        Price = x.Price,
                        Notes = x.Notes ?? string.Empty,
                        OrderItemModifiers = _mapper.Map<List<PhoneCallOrderItemModifiers>>(x.OrderItemModifiers),
                        Status = PosOrderItemStatus.Added
                    }).ToList());
                    
                    continue;
                }

                if (originalList.Count != 0 && newList.Count != 0)
                {
                    Log.Information("originalList: {@OriginalList} and newList: {@NewList} have different count", originalList, newList);
                    
                    var matchedItems = new List<PosOrderItemDto>();
                    foreach (var newItem in newList)
                    {
                        var strictItem = originalList.FirstOrDefault(o =>
                            !matchedItems.Contains(o) && !string.IsNullOrWhiteSpace(o.Notes) && !string.IsNullOrWhiteSpace(newItem.Notes) && newItem.Notes.Trim() == o.Notes.Trim());

                        if (strictItem != null)
                        {
                            Log.Information("Strict match item: {@StrictItem}", strictItem);
                            matchedItems.Add(strictItem);
                            
                            result.Add(new PosOrderItemDto
                            {
                                Id = newItem.Id,
                                ProductId = newItem.ProductId,
                                Quantity = newItem.Quantity,
                                OriginalPrice = newItem.OriginalPrice,
                                Price = newItem.Price,
                                Notes = newItem.Notes ?? string.Empty,
                                OrderItemModifiers = _mapper.Map<List<PhoneCallOrderItemModifiers>>(newItem.OrderItemModifiers),
                                Status = newItem.Quantity > 0 ? PosOrderItemStatus.Normal : PosOrderItemStatus.Missed
                            });
                            
                            continue;
                        }
 
                        var fuzzyItem = originalList.FirstOrDefault(o => !matchedItems.Contains(o) && !string.IsNullOrWhiteSpace(o.Notes) == !string.IsNullOrWhiteSpace(newItem.Notes));
                        if (fuzzyItem != null)
                        {
                            Log.Information("Fuzzy match item: {@FuzzyItem}", fuzzyItem);
                            
                            matchedItems.Add(fuzzyItem);
                            
                            result.Add(new PosOrderItemDto
                            {
                                Id = newItem.Id,
                                ProductId = newItem.ProductId,
                                Quantity = newItem.Quantity,
                                OriginalPrice = newItem.OriginalPrice,
                                Price = newItem.Price,
                                Notes = newItem.Notes ?? string.Empty,
                                OrderItemModifiers = _mapper.Map<List<PhoneCallOrderItemModifiers>>(newItem.OrderItemModifiers),
                                Status = newItem.Quantity > 0 ? PosOrderItemStatus.Normal : PosOrderItemStatus.Missed
                            });
                            
                            continue;
                        }
                        
                        result.Add(new PosOrderItemDto
                        {
                            Id = newItem.Id,
                            ProductId = newItem.ProductId,
                            Quantity = newItem.Quantity,
                            OriginalPrice = newItem.OriginalPrice,
                            Price = newItem.Price,
                            Notes = newItem.Notes ?? string.Empty,
                            OrderItemModifiers = _mapper.Map<List<PhoneCallOrderItemModifiers>>(newItem.OrderItemModifiers),
                            Status = PosOrderItemStatus.Added
                        });
                    }
                    
                    Log.Information("Handle no matching items: {@NoMatchingItems}", originalList.Except(matchedItems).ToList());
                    
                    result.AddRange(originalList.Except(matchedItems).Select(x => new PosOrderItemDto
                    {
                        Id = x.Id,
                        ProductId = x.ProductId,
                        Quantity = x.Quantity,
                        OriginalPrice = x.OriginalPrice,
                        Price = x.Price,
                        Notes = x.Notes ?? string.Empty,
                        OrderItemModifiers = _mapper.Map<List<PhoneCallOrderItemModifiers>>(x.OrderItemModifiers),
                        Status = PosOrderItemStatus.Missed
                    }));
                }
                
                Log.Information("Current modified items: {@Result}", result);
            }
        }

        return result;
    }

    private async Task<PosOrder> GetOrAddPosOrderAsync(PlacePosOrderCommand command, CancellationToken cancellationToken)
    {
        PosOrder order;

        if (command.OrderId.HasValue)
        {
            order = await _posDataProvider.GetPosOrderByIdAsync(orderId: command.OrderId.Value, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            if (order == null) throw new Exception("Order could not be found.");
            
            _mapper.Map(command, order);
        }
        else
        {
            order = _mapper.Map<PosOrder>(command);
            order.Status = PosOrderStatus.Pending;
            
            order.OrderNo = await GenerateOrderNumberAsync(order.StoreId, cancellationToken).ConfigureAwait(false);
            
            await _posDataProvider.AddPosOrdersAsync([order], cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        
        return order;
    }
    
    private async Task<string> GenerateOrderNumberAsync(int storeId, CancellationToken cancellationToken)
    {
        return await _redisSafeRunner.ExecuteWithLockAsync($"generate-order-number-{storeId}", async() =>
        {
            var store = await _posDataProvider.GetPosCompanyStoreAsync(id: storeId, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (store == null) throw new Exception("Store could not be found.");

            var (utcStart,utcEnd) = GetUtcMidnightForTimeZone(DateTimeOffset.UtcNow, store.Timezone);
            
            var preOrder = await _posDataProvider.GetPosOrderSortByOrderNoAsync(storeId, utcStart, utcEnd, cancellationToken: cancellationToken).ConfigureAwait(false);
        
            Log.Information("Get store pre order: {@Order} by store id: {storeId}", preOrder, storeId);
            
            if (preOrder == null) return "0001";

            var rs = Convert.ToInt32(preOrder.OrderNo);
        
            rs++;
        
            return rs.ToString("D4");
        }, wait: TimeSpan.FromSeconds(10), retry: TimeSpan.FromSeconds(1), server: RedisServer.System).ConfigureAwait(false);
    }

    private string TimezoneMapping(string timezone)
    {
        if (string.IsNullOrEmpty(timezone)) return "Pacific Standard Time";
        
        return timezone.Trim() switch
        {
            "America/Los_Angeles" => "Pacific Standard Time",
            _ => timezone.Trim()
        };
    }
    
    private (DateTimeOffset utcStart, DateTimeOffset utcEnd) GetUtcMidnightForTimeZone(DateTimeOffset utcNow, string timezone)
    {
        var windowsId = TimezoneMapping(timezone);
        var tz = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        
        var localTime = TimeZoneInfo.ConvertTime(utcNow, tz);
        var localMidnight = new DateTime(localTime.Year, localTime.Month, localTime.Day, 0, 0, 0);
        var localStart = new DateTimeOffset(localMidnight, tz.GetUtcOffset(localMidnight));
        
        var utcStart = localStart.ToUniversalTime();
        var utcEnd = utcStart.AddDays(1);
        
        return (utcStart, utcEnd);
    }
    
    private async Task<string> GetPosTokenAsync(PosCompanyStore store, PosOrder order, CancellationToken cancellationToken)
    {
        var authorization = await _easyPosClient.GetEasyPosTokenAsync(new EasyPosTokenRequestDto
        {
            BaseUrl = store.Link,
            AppId = store.AppId,
            AppSecret = store.AppSecret
        }, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Getting the store pos token {Success} is available: {IsAvailable}", authorization?.Success, !string.IsNullOrEmpty(authorization?.Data));
        
        return authorization?.Data;
    }

    private async Task<(bool IsAvailable, PosOrderStatus Status)> ValidatePosProductsAsync(PosOrder order, PosCompanyStore store, string token, CancellationToken cancellationToken)
    {
        try
        {
            Log.Information("The pos token is null: {IsNull}", string.IsNullOrEmpty(token));
            
            if (string.IsNullOrEmpty(token)) return (false, PosOrderStatus.Error);
            
            var items = JsonConvert.DeserializeObject<List<PosOrderItemDto>>(order.Items);

            var response = await _easyPosClient.ValidatePosProductsAsync(new ValidatePosProductRequestDto
            {
                ProductIds = items.Select(x => x.ProductId).ToList(),
            }, store.Link, token, cancellationToken).ConfigureAwait(false);
        
            Log.Information("Validating pos products response: {@Response}", response);

            if (response?.Data == null)
            {
                Log.Error("Failed to get pos products");
                
                return (false, PosOrderStatus.Error);
            }

            if (response.Data.Count == 0) return (true, PosOrderStatus.Pending);
        
            foreach (var item in items)
            {
                var result = response.Data.Any(x => x == item.ProductId);

                if (!result) continue;

                item.Status = PosOrderItemStatus.Missed;
            }
        
            order.ModifiedItems = JsonConvert.SerializeObject(items);
        
            return (false, PosOrderStatus.Modified);
        }
        catch (Exception e)
        {
            Log.Information($"Failed to validate pos products: {e.Message}");

            return (false, PosOrderStatus.Error);
        }
    }

    public async Task SafetyPlaceOrderAsync(PosOrder order, PosCompanyStore store, string token, bool isWithRetry, int retryCount, CancellationToken cancellationToken)
    {
        const int MaxRetryCount = 3;
        var lockKey = $"place-order-key-{order.Id}";
        
        await _redisSafeRunner.ExecuteWithLockAsync(lockKey, async () =>
        {
            if(order.Status == PosOrderStatus.Sent) throw new Exception("Order is already sent.");

            if (isWithRetry) order.RetryCount = retryCount;
            
            var (isAvailable, status) = await ValidatePosProductsAsync(order, store, token, cancellationToken).ConfigureAwait(false);

            if (!isAvailable)
            {
                Log.Information("Current token is available: {IsAvailable}", string.IsNullOrWhiteSpace(token));
                token = !string.IsNullOrWhiteSpace(token) ? token : await GetPosTokenAsync(store, order, cancellationToken).ConfigureAwait(false);
                
                await MarkOrderAsSpecificStatusAsync(order, status, cancellationToken).ConfigureAwait(false);

                if (status == PosOrderStatus.Error && isWithRetry && order.RetryCount < MaxRetryCount)
                    _smartTalkBackgroundJobClient.Schedule(() => SafetyPlaceOrderAsync(
                        order, store, token, true, order.RetryCount + 1, cancellationToken), TimeSpan.FromSeconds(30), HangfireConstants.InternalHostingRestaurant);
                
                return;
            }
            
            await SafetyPlaceOrderWithRetryAsync(order, store, token, isWithRetry, cancellationToken).ConfigureAwait(false);
            
        }, wait: TimeSpan.FromSeconds(10), retry: TimeSpan.FromSeconds(1), server: RedisServer.System).ConfigureAwait(false);
    }

    private async Task<PlaceOrderToEasyPosResponseDto> PlaceOrderAsync(PosOrder order, PosCompanyStore store, string token, CancellationToken cancellationToken)
    {
        var response = await _easyPosClient.PlaceOrderAsync(new PlaceOrderToEasyPosRequestDto
        {
            Type = order.Type == PosOrderReceiveType.Pickup ? 1 : 3,
            Guests = 1,
            IsTaxFree = false,
            Notes = string.IsNullOrEmpty(order.Notes) ? string.Empty : order.Notes,
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
        }, store.Link, token, order.RetryCount <= 0 ? TimeSpan.FromSeconds(10) : null, cancellationToken).ConfigureAwait(false);

        Log.Information("Place order: {@Order} and response is: {@Response}", order, response);
        
        return response;
    }

    private async Task SafetyPlaceOrderWithRetryAsync(PosOrder order, PosCompanyStore store, string token, bool isRetry, CancellationToken cancellationToken)
    {
        const int MaxRetryCount = 3;
        
        try
        {
            var response = await PlaceOrderAsync(order, store, token, cancellationToken).ConfigureAwait(false);

            if (response is { Success: true })
            {
                order.OrderId = response.Data?.Order?.Id.ToString() ?? string.Empty;
                
                await MarkOrderAsSpecificStatusAsync(order, PosOrderStatus.Sent, cancellationToken).ConfigureAwait(false);
                
                return;
            }

            await MarkOrderAsSpecificStatusAsync(order, PosOrderStatus.Error, cancellationToken).ConfigureAwait(false);

            if (isRetry && order.RetryCount < MaxRetryCount)
                _smartTalkBackgroundJobClient.Schedule(() => SafetyPlaceOrderAsync(
                    order, store, token, true, order.RetryCount + 1, cancellationToken), TimeSpan.FromSeconds(30), HangfireConstants.InternalHostingRestaurant);
        }
        catch (Exception ex)
        {
            await MarkOrderAsSpecificStatusAsync(order, PosOrderStatus.Error, cancellationToken).ConfigureAwait(false);
            
            if (isRetry && order.RetryCount < MaxRetryCount)
                _smartTalkBackgroundJobClient.Schedule(() => SafetyPlaceOrderAsync(
                    order, store, token, true, order.RetryCount + 1, cancellationToken), TimeSpan.FromSeconds(30), HangfireConstants.InternalHostingRestaurant);

            Log.Information("Place order {@Order} failed: {@Exception}", order, ex);
        }
    }
    
    private async Task MarkOrderAsSpecificStatusAsync(PosOrder order, PosOrderStatus status, CancellationToken cancellationToken)
    {
        order.Status = status;
        
        if(status == PosOrderStatus.Sent) order.IsPush = true;
        
        await _posDataProvider.UpdatePosOrdersAsync([order], cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private PosOrderModifiedStatus PosOrderModifiedStatusMapping(int? status)
    {
        if (!status.HasValue) return PosOrderModifiedStatus.Normal;
        
        return status switch
        {
            3 => PosOrderModifiedStatus.BeMerged,
            2 => PosOrderModifiedStatus.Cancelled,
            _ => PosOrderModifiedStatus.Normal
        };
    }
}