using Serilog;
using Newtonsoft.Json;
using System.Globalization;
using Mediator.Net;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.Printer;
using SmartTalk.Messages.Commands.Pos;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Dto.Printer;
using SmartTalk.Messages.Enums.Caching;
using SmartTalk.Messages.Enums.Pos;
using SmartTalk.Messages.Enums.Printer;
using SmartTalk.Messages.Events.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Services.Pos;

public partial interface IPosService
{
    Task<GetPosStoreOrdersResponse> GetPosStoreOrdersAsync(GetPosStoreOrdersRequest request, CancellationToken cancellationToken = default);
    
    Task<PosOrderPlacedEvent> PlacePosStoreOrdersAsync(PlacePosOrderCommand command, CancellationToken cancellationToken = default);
    
    Task UpdatePosOrderAsync(UpdatePosOrderCommand command, CancellationToken cancellationToken);
    
    Task<GetPosOrderProductsResponse> GetPosOrderProductsAsync(GetPosOrderProductsRequest request, CancellationToken cancellationToken);
    
    Task<GetPosStoreOrderResponse> GetPosStoreOrderAsync(GetPosStoreOrderRequest request, CancellationToken cancellationToken);

    Task HandlePosOrderAsync(PosOrder order, bool isRetry, CancellationToken cancellationToken);

    Task<UpdatePosOrderPrintStatusResponse> UpdatePosOrderPrintStatusAsync(UpdatePosOrderPrintStatusCommand command, CancellationToken cancellationToken);
    
    Task<GetPrintStatusResponse> GetPrintStatusAsync(GetPrintStatusRequest request, CancellationToken cancellationToken);
    
    Task<GetPosOrderCloudPrintStatusResponse> GetPosOrderCloudPrintStatusAsync(GetPosOrderCloudPrintStatusRequest request, CancellationToken cancellationToken);

    Task RetryCloudPrintingAsync(RetryCloudPrintingCommand command, CancellationToken cancellationToken);
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

    public async Task<PosOrderPlacedEvent> PlacePosStoreOrdersAsync(PlacePosOrderCommand command, CancellationToken cancellationToken = default)
    {
        var order = await GetOrAddPosOrderAsync(command, cancellationToken).ConfigureAwait(false);
        
        await HandlePosOrderAsync(order, command.IsWithRetry, cancellationToken).ConfigureAwait(false);
        
        return new PosOrderPlacedEvent
        {
            Order = _mapper.Map<PosOrderDto>(order)
        };
    }

    public async Task HandlePosOrderAsync(PosOrder order, bool isRetry, CancellationToken cancellationToken)
    {
        var store = await _posDataProvider.GetPosCompanyStoreAsync(id: order.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (store == null) throw new Exception("Store could not be found.");

        var token = await GetPosTokenAsync(store, cancellationToken).ConfigureAwait(false);

        if (!store.IsLink && string.IsNullOrWhiteSpace(token))
        {
            order.Status = PosOrderStatus.Modified;
        
            await _posDataProvider.UpdatePosOrdersAsync([order], cancellationToken: cancellationToken).ConfigureAwait(false);
        
            return;
        }
    
        await SafetyPlaceOrderAsync(order.Id, store, token, isRetry, 0, cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateMerchPrinterOrderAsync(int storeId, int orderId, PosOrder order, CancellationToken cancellationToken)
    {
        Log.Information("storeId:{storeId}, orderId:{orderId}", storeId, orderId);
        
        var merchPrinter = (await _printerDataProvider.GetMerchPrintersAsync(storeId: storeId, isEnabled: true, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        Log.Information("get merch printer:{@merchPrinter}", merchPrinter);

        var merchPrinterOrder = new MerchPrinterOrder
        {
            OrderId = orderId,
            StoreId = storeId,
            PrinterMac = merchPrinter?.PrinterMac,
            PrintDate = DateTimeOffset.Now,
            PrintFormat = PrintFormat.Order
        };
        
        await _printerDataProvider.AddMerchPrinterOrderAsync(merchPrinterOrder, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Create merch printer order:{@merchPrinterOrder}", merchPrinterOrder);

        _smartTalkBackgroundJobClient.Schedule<IMediator>( x => x.SendAsync(new RetryCloudPrintingCommand{ Id = merchPrinterOrder.Id, Count = 0}, CancellationToken.None), TimeSpan.FromSeconds(10));
    }

    public async Task RetryCloudPrintingAsync(RetryCloudPrintingCommand command, CancellationToken cancellationToken)
    {
        Log.Information("retry cloud printer:{id}", command.Id);
        
        var merchPrinterOrder = (await _printerDataProvider.GetMerchPrinterOrdersAsync(id: command.Id, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        if (merchPrinterOrder == null) return;
        
        if (merchPrinterOrder.PrintStatus != PrintStatus.Printed)
        {
            var merchPrinter = (await _printerDataProvider.GetMerchPrintersAsync(storeId: merchPrinterOrder.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
            
            merchPrinterOrder.PrintStatus = PrintStatus.Waiting;
            merchPrinterOrder.PrinterMac = merchPrinter?.PrinterMac;

            await _printerDataProvider.UpdateMerchPrinterOrderAsync(merchPrinterOrder, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else return;

        if (command.Count < 3)
            _smartTalkBackgroundJobClient.Schedule<IMediator>( x => x.SendAsync(new RetryCloudPrintingCommand{ Id = merchPrinterOrder.Id, Count = command.Count + 1 },  CancellationToken.None), TimeSpan.FromSeconds(30));
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
        var products = await _posDataProvider.GetPosProductsByProductIdsAsync(
            request.StoreId, request.ProductIds, cancellationToken).ConfigureAwait(false);
        
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
        {
            Log.Information($"Order could not be found by orderId: {request.OrderId} or recordId: {request.RecordId}.");
            
            return new GetPosStoreOrderResponse();
        }

        var enrichOrder = _mapper.Map<PosOrderDto>(order);
        
        await EnrichPosOrderAsync(enrichOrder, request.IsWithSpecifications, cancellationToken).ConfigureAwait(false);

        return new GetPosStoreOrderResponse { Data = enrichOrder };
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
    
    private async Task<string> GetPosTokenAsync(CompanyStore store, CancellationToken cancellationToken)
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

    private async Task<(bool IsAvailable, PosOrderStatus Status)> ValidatePosProductsAsync(PosOrder order, CompanyStore store, string token, CancellationToken cancellationToken)
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

    public async Task SafetyPlaceOrderAsync(int orderId, CompanyStore store, string token, bool isWithRetry, int retryCount, CancellationToken cancellationToken)
    {
        const int MaxRetryCount = 3;
        var lockKey = $"place-order-key-{orderId}";
        
        await _redisSafeRunner.ExecuteWithLockAsync(lockKey, async () =>
        {
            var order = await _posDataProvider.GetPosOrderByIdAsync(orderId: orderId, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            if (order.Status == PosOrderStatus.Sent)
            {
                Log.Information("Order {OrderId} is already sent, skip placing again.", order.Id);
                
                return;
            }

            if (isWithRetry) order.RetryCount = retryCount;
            
            var (isAvailable, status) = await ValidatePosProductsAsync(order, store, token, cancellationToken).ConfigureAwait(false);

            if (!isAvailable)
            {
                Log.Information("Current token is available: {IsAvailable}", string.IsNullOrWhiteSpace(token));
                token = !string.IsNullOrWhiteSpace(token) ? token : await GetPosTokenAsync(store, cancellationToken).ConfigureAwait(false);
                
                await MarkOrderAsSpecificStatusAsync(order, status, cancellationToken).ConfigureAwait(false);

                if (status == PosOrderStatus.Error && isWithRetry && order.RetryCount < MaxRetryCount)
                    _smartTalkBackgroundJobClient.Schedule(() => SafetyPlaceOrderAsync(
                        order.Id, store, token, true, order.RetryCount + 1, cancellationToken), TimeSpan.FromSeconds(30), HangfireConstants.InternalHostingRestaurant);
                
                return;
            }
            
            await SafetyPlaceOrderWithRetryAsync(order, store, token, isWithRetry, cancellationToken).ConfigureAwait(false);
            
            if (order.Status is PosOrderStatus.Sent or PosOrderStatus.Modified)
                _smartTalkBackgroundJobClient.Enqueue(() => CreateMerchPrinterOrderAsync(store.Id, order.Id, order, cancellationToken));
            
        }, wait: TimeSpan.Zero, retry: TimeSpan.Zero, server: RedisServer.System).ConfigureAwait(false);
    }

    public async Task<UpdatePosOrderPrintStatusResponse> UpdatePosOrderPrintStatusAsync(UpdatePosOrderPrintStatusCommand command, CancellationToken cancellationToken)
    {
        var order = await _posDataProvider.GetPosOrderByIdAsync(posOrderId: command.OrderId, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (order == null)
            throw new Exception("Order could not be found.");

        order.IsPrinted = PosOrderIsPrintStatus.Sending;
        
        await _posDataProvider.UpdatePosOrdersAsync([order], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var store = await _posDataProvider.GetPosCompanyStoreAsync(id: command.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (store == null || string.IsNullOrEmpty(store.Link) || string.IsNullOrEmpty(store.AppId) || string.IsNullOrEmpty(store.AppSecret))
            throw new Exception("Store could not be found or appId、url、secret could not be empty.");
        
        var response = await _easyPosClient.GetPosOrderAsync(new GetOrderRequestDto
        {
            BaseUrl = store.Link,
            AppId = store.AppId,
            AppSecret = store.AppSecret,
            OrderId = long.Parse(command.OrderId)
        }, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get pos order response: {@Response}", response);

        if (response?.Data?.Order == null || response.Success == false)
            throw new Exception($"Order {command.OrderId} could not be found.");
        
        var firstTime = DateTimeOffset.Now;
        var timeout = TimeSpan.FromSeconds(10);
        
        while (response.Data.Order.IsPrinted != true && DateTimeOffset.Now - firstTime < timeout)
        {
            response = await _easyPosClient.GetPosOrderAsync(new GetOrderRequestDto
            {
                BaseUrl = store.Link,
                AppId = store.AppId,
                AppSecret = store.AppSecret,
                OrderId = long.Parse(command.OrderId)
            }, cancellationToken).ConfigureAwait(false);
        
            Log.Information("Retry get pos order response: {@Response}", response);
            
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }
        
        order.IsPrinted = response.Data.Order.IsPrinted ? PosOrderIsPrintStatus.Successed : PosOrderIsPrintStatus.Failed;
        
        await _posDataProvider.UpdatePosOrdersAsync([order], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (command.RetryCount < 3 && response.Data.Order.IsPrinted != true)
        {
            _smartTalkBackgroundJobClient.Schedule<IMediator>(m => m.SendAsync(new UpdatePosOrderPrintStatusCommand
            {
                StoreId = command.StoreId,
                OrderId = command.OrderId,
                RetryCount = command.RetryCount + 1
            }, cancellationToken), TimeSpan.FromSeconds(30));
        }
        
        return new UpdatePosOrderPrintStatusResponse
        {
            Data = _mapper.Map<PosOrderDto>(order)
        };
    }
    
    public async Task<GetPrintStatusResponse> GetPrintStatusAsync(GetPrintStatusRequest request, CancellationToken cancellationToken)
    {
        var order = await _posDataProvider.GetPosOrderByIdAsync(orderId: request.OrderId, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (order == null)
            throw new Exception("Order could not be found.");
        
        return new GetPrintStatusResponse
        {
            Data = _mapper.Map<PosOrderDto>(order)
        };
    }

    private async Task<PlaceOrderToEasyPosResponseDto> PlaceOrderAsync(PosOrder order, CompanyStore store, string token, CancellationToken cancellationToken)
    {
        var response = await _easyPosClient.PlaceOrderAsync(new PlaceOrderToEasyPosRequestDto
        {
            Type = order.Type == PosOrderReceiveType.Pickup ? 1 : 3,
            Guests = 1,
            IsTaxFree = false,
            Notes = string.IsNullOrEmpty(order.Notes) ? string.IsNullOrEmpty(order.Remarks) ? string.Empty : order.Remarks : order.Notes,
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

    private async Task SafetyPlaceOrderWithRetryAsync(PosOrder order, CompanyStore store, string token, bool isRetry, CancellationToken cancellationToken)
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
                    order.Id, store, token, true, order.RetryCount + 1, cancellationToken), TimeSpan.FromSeconds(30), HangfireConstants.InternalHostingRestaurant);
        }
        catch (Exception ex)
        {
            await MarkOrderAsSpecificStatusAsync(order, PosOrderStatus.Error, cancellationToken).ConfigureAwait(false);
            
            if (isRetry && order.RetryCount < MaxRetryCount)
                _smartTalkBackgroundJobClient.Schedule(() => SafetyPlaceOrderAsync(
                    order.Id, store, token, true, order.RetryCount + 1, cancellationToken), TimeSpan.FromSeconds(30), HangfireConstants.InternalHostingRestaurant);

            Log.Information("Place order {@Order} failed: {@Exception}", order, ex);
        }
    }
    
    private async Task MarkOrderAsSpecificStatusAsync(PosOrder order, PosOrderStatus status, CancellationToken cancellationToken)
    {
        order.Status = status;
        order.SentBy = _currentUser.Id;
        order.SentTime = DateTimeOffset.Now;
        
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

    private async Task EnrichPosOrderAsync(PosOrderDto order, bool isWithSpecifications = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var simpleModifiers = new List<PosProductSimpleModifiersDto>();
            
            var items = JsonConvert.DeserializeObject<List<PosOrderItemDto>>(order.Items);
        
            var products = await _posDataProvider.GetPosProductsAsync(
                productIds: items.Select(x => x.ProductId.ToString()).Distinct().ToList(), cancellationToken: cancellationToken).ConfigureAwait(false);
            
            var productsLookup = products.GroupBy(x => x.ProductId).ToDictionary(
                g => g.Key, g =>
                {
                    var p = g.First();
                    
                    return (p.Names, string.IsNullOrWhiteSpace(p.Modifiers) ? [] : JsonConvert.DeserializeObject<List<EasyPosResponseModifierGroups>>(p.Modifiers));
                });

            foreach (var item in items)
            {
                if (productsLookup.TryGetValue(item.ProductId.ToString(), out var product))
                {
                    item.ProductName = product.Names;

                    if (isWithSpecifications && product.Item2.Count > 0)
                    {
                        var matchedModifiers = simpleModifiers.Where(x => x.ProductId == item.ProductId.ToString()).ToList();

                        if (matchedModifiers.Count > 0) continue;
                        
                        simpleModifiers.AddRange(product.Item2.Select(x => new PosProductSimpleModifiersDto
                        {
                            ProductId = item.ProductId.ToString(),
                            ModifierId = x.Id.ToString(),
                            MinimumSelect = x.MinimumSelect,
                            MaximumSelect = x.MaximumSelect,
                            MaximumRepetition = x.MaximumRepetition,
                            ModifierProductIds = x.ModifierProducts.Select(m => m.Id.ToString()).ToList()
                        }));
                    }
                }
                else
                    item.ProductName = null;
            }
            
            order.SimpleModifiers = simpleModifiers;
            order.Items = JsonConvert.SerializeObject(items);

            var merchPrinterOrder = (await _printerDataProvider.GetMerchPrinterOrdersAsync(orderId: order.Id, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
            MerchPrinterDto merchPrinterDto = null;
            order.CloudPrintOrderId = merchPrinterOrder?.Id;
            order.CloudPrintStatus = CloudPrintStatus.Failed;
        
            if (merchPrinterOrder is { PrinterMac: not null })
            {
                var merchPrinterLog = (await _printerDataProvider.GetMerchPrinterLogAsync(storeId: order.StoreId, orderId: order.Id, cancellationToken: cancellationToken).ConfigureAwait(false)).Item2.FirstOrDefault();
                var merchPrinter = (await _printerDataProvider.GetMerchPrintersAsync(printerMac: merchPrinterOrder.PrinterMac).ConfigureAwait(false)).FirstOrDefault();

                if (merchPrinterOrder.PrintStatus == PrintStatus.Printed && merchPrinterLog is { Code: 200 })
                    order.CloudPrintStatus = CloudPrintStatus.Successful;
                else if (merchPrinterOrder.PrintStatus is PrintStatus.Waiting or PrintStatus.Printing && merchPrinter is { IsEnabled: true })
                {
                    merchPrinterDto = _mapper.Map<MerchPrinterDto>(merchPrinter);

                    if (merchPrinterDto.PrinterStatusInfo.Online && merchPrinterDto.PrinterStatusInfo.PaperEmpty == false)
                    {
                        order.CloudPrintStatus = CloudPrintStatus.Printing;
                    }
                }
            }
            
            var company = await _posDataProvider.GetPosCompanyStoreAsync(id: order.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);

            order.IsLink = company.IsLink;
            order.IsLinkCouldPrinting = merchPrinterDto != null && merchPrinterDto.PrinterStatusInfo.Online;
            
            if (!order.SentBy.HasValue) return;
            
            var userAccount = await _accountDataProvider.GetUserAccountByUserIdAsync(order.SentBy.Value, cancellationToken).ConfigureAwait(false);

            order.SentByUsername = userAccount.UserName;
        }
        catch (Exception e)
        {
            Log.Information("Enriching pos order failed: {@Exception}", e);
        }
    }
    
    public async Task<GetPosOrderCloudPrintStatusResponse> GetPosOrderCloudPrintStatusAsync(GetPosOrderCloudPrintStatusRequest request, CancellationToken cancellationToken)
    {
        var merchPrinterOrder = (await _printerDataProvider.GetMerchPrinterOrdersAsync(orderId: request.OrderId, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        
        var cloudPrintStatus = CloudPrintStatus.Failed;
        
        if (merchPrinterOrder != null && merchPrinterOrder.PrinterMac != null)
        {
            var merchPrinterLog = (await _printerDataProvider.GetMerchPrinterLogAsync(storeId: request.StoreId, orderId: request.OrderId, cancellationToken: cancellationToken).ConfigureAwait(false)).Item2.FirstOrDefault();
            var merchPrinter = (await _printerDataProvider.GetMerchPrintersAsync(printerMac: merchPrinterOrder.PrinterMac).ConfigureAwait(false)).FirstOrDefault();
            
            if (merchPrinterOrder.PrintStatus == PrintStatus.Printed && merchPrinterLog is { Code: 200 })
                cloudPrintStatus = CloudPrintStatus.Successful;
            else if (merchPrinterOrder.PrintStatus is PrintStatus.Waiting or PrintStatus.Printing && merchPrinter is { IsEnabled: true })
            {
                var merchPrinterDto = _mapper.Map<MerchPrinterDto>(merchPrinter);

                if (merchPrinterDto.PrinterStatusInfo.Online && merchPrinterDto.PrinterStatusInfo.PaperEmpty == false)
                {
                    cloudPrintStatus = CloudPrintStatus.Printing;
                }
            }else
                cloudPrintStatus = CloudPrintStatus.Failed;
        }

        return new GetPosOrderCloudPrintStatusResponse
        {
           Data = new GetPosOrderCloudPrintStatusDto
           {
               Id = merchPrinterOrder?.Id,
               CloudPrintStatus = cloudPrintStatus
           }
        };
    }
}