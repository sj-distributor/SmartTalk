using Serilog;
using Newtonsoft.Json;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Enums.VoiceAi;
using SmartTalk.Messages.Enums.Caching;
using SmartTalk.Core.Domain.VoiceAi.PosManagement;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;
using SmartTalk.Messages.Requests.VoiceAi.PosManagement;

namespace SmartTalk.Core.Services.VoiceAi.PosManagement;

public partial interface IPosManagementService
{
    Task<GetPosStoreOrdersResponse> GetPosStoreOrdersAsync(GetPosStoreOrdersRequest request, CancellationToken cancellationToken = default);
    
    Task<PlacePosOrderResponse> PlacePosStoreOrdersAsync(PlacePosOrderCommand command, CancellationToken cancellationToken = default);
}

public partial class PosManagementService
{
    public async Task<GetPosStoreOrdersResponse> GetPosStoreOrdersAsync(GetPosStoreOrdersRequest request, CancellationToken cancellationToken = default)
    {
        var storeOrders = await _posManagementDataProvider.GetPosOrdersAsync(
            request.StoreId, request.Keyword, request.StartDate, request.EndDate, cancellationToken).ConfigureAwait(false);

        return new GetPosStoreOrdersResponse
        {
            Data = _mapper.Map<List<PosOrderDto>>(storeOrders)
        };
    }

    public async Task<PlacePosOrderResponse> PlacePosStoreOrdersAsync(PlacePosOrderCommand command, CancellationToken cancellationToken = default)
    {
        var order = await _posManagementDataProvider.GetPosOrderByIdAsync(command.OrderId, cancellationToken).ConfigureAwait(false);

        if (order == null) throw new Exception("Order could not be found.");
        
        var token = await GetPosTokenAsync(order.StoreId, cancellationToken).ConfigureAwait(false);
        
        await SafetyPlaceOrderAsync(order, token, command.OrderItems, command.IsWithRetry, cancellationToken).ConfigureAwait(false);

        return new PlacePosOrderResponse
        {
            Data = _mapper.Map<PosOrderDto>(order)
        };
    }

    private async Task<string> GetPosTokenAsync(int storeId, CancellationToken cancellationToken)
    {
        var store = await _posManagementDataProvider.GetPosCompanyStoreAsync(id: storeId, cancellationToken: cancellationToken).ConfigureAwait(false);

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

    private async Task SafetyPlaceOrderAsync(PosOrder order, string token, string orderItems, bool isWithRetry, CancellationToken cancellationToken)
    {
        var lockKey = $"place-order-key-{order.Id}";
        await _redisSafeRunner.ExecuteWithLockAsync(lockKey, async () =>
        {
            if(order.Status == PosOrderStatus.Sent) throw new Exception("Order is already sent.");
        
            order.Items = orderItems;

            var orderId = isWithRetry
                ? await SafetyPlaceOrderWithRetryIfRequiredAsync(order, token, cancellationToken).ConfigureAwait(false)
                : await SafetyPlaceOrderIfRequiredAsync(order, token, cancellationToken).ConfigureAwait(false);
        
            order.OrderId = orderId;
            
            await _posManagementDataProvider.UpdatePosOrdersAsync([order], cancellationToken: cancellationToken).ConfigureAwait(false);
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
                    Type = 1,
                    IsTaxFree = false,
                    Notes = order.Notes,
                    OrderItems = JsonConvert.DeserializeObject<List<PhoneCallOrderItem>>(order.Items),
                    Customer = new PhoneCallOrderCustomer
                    {
                        Name = order.Name,
                        Phone = order.Phone
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
                Notes = order.Notes,
                OrderItems = JsonConvert.DeserializeObject<List<PhoneCallOrderItem>>(order.Items),
                Customer = new PhoneCallOrderCustomer
                {
                    Name = order.Name,
                    Phone = order.Phone
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
        
        await _posManagementDataProvider.UpdatePosOrdersAsync([order], cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}