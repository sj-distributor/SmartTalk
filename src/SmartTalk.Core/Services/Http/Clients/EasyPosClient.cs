using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.EasyPos;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.EasyPos;

namespace SmartTalk.Core.Services.Http.Clients;

public interface IEasyPosClient : IScopedDependency
{
    Task<EasyPosResponseDto> GetEasyPosRestaurantMenusAsync(string restaurantName, CancellationToken cancellationToken);

    Task<GetOrderResponse> GetOrderAsync(long id, string restaurantName, CancellationToken cancellationToken);
    
    Task<PlaceOrderToEasyPosResponseDto> PlaceOrderToEasyPosAsync(PlaceOrderToEasyPosRequestDto request, CancellationToken cancellationToken = default);

    Task<EasyPosTokenResponseDto> GetEasyPosTokenAsync(EasyPosTokenRequestDto request, CancellationToken cancellationToken);
    
    Task<EasyPosResponseDto> GetPosCompanyStoreMenusAsync(EasyPosTokenRequestDto request, CancellationToken cancellationToken);

    Task<EasyPosMerchantResponseDto> GetPosCompanyStoreMessageAsync(EasyPosTokenRequestDto request, CancellationToken cancellationToken);
    
    Task<PlaceOrderToEasyPosResponseDto> PlaceOrderAsync(PlaceOrderToEasyPosRequestDto request, string token, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}

public class EasyPosClient : IEasyPosClient
{
    private readonly EasyPosSetting _easyPosSetting;
    private readonly ISmartiesHttpClientFactory _httpClientFactory;

    public EasyPosClient(EasyPosSetting easyPosSetting, ISmartiesHttpClientFactory httpClientFactory)
    {
        _easyPosSetting = easyPosSetting;
        _httpClientFactory = httpClientFactory;
    }
    
    public async Task<EasyPosResponseDto> GetEasyPosRestaurantMenusAsync(string restaurantName, CancellationToken cancellationToken)
    {
        var (authorization, merchantId, companyId, merchantStaffId) = GetRestaurantAuthHeaders(restaurantName);
        
        return await _httpClientFactory.GetAsync<EasyPosResponseDto>(
            requestUrl: $"{_easyPosSetting.BaseUrl}/api/merchant/resource", headers: new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {authorization}"},
                { "MerchantId", merchantId },
                { "CompanyId", companyId },
                { "MerchantStaffId", merchantStaffId }
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<GetOrderResponse> GetOrderAsync(long id, string restaurantName, CancellationToken cancellationToken)
    {
        var (authorization, merchantId, companyId, merchantStaffId) = GetRestaurantAuthHeaders(restaurantName);
        
        return await _httpClientFactory.GetAsync<GetOrderResponse>(
            requestUrl: $"{_easyPosSetting.BaseUrl}/api/merchant/order?id={ id }", headers: new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {authorization}"},
                { "MerchantId", merchantId },
                { "CompanyId", companyId },
                { "MerchantStaffId", merchantStaffId }
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<PlaceOrderToEasyPosResponseDto> PlaceOrderToEasyPosAsync(PlaceOrderToEasyPosRequestDto request, CancellationToken cancellationToken = default)
    {
        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {_easyPosSetting.MoonHouseAuthorization}" },
            { "MerchantId", _easyPosSetting.MoonHouseMerchantId },
            { "CompanyId", _easyPosSetting.MoonHouseCompanyId },
            { "MerchantStaffId", _easyPosSetting.MoonHouseMerchantStaffId }
        };

        return await _httpClientFactory.PostAsJsonAsync<PlaceOrderToEasyPosResponseDto>(
            $"{_easyPosSetting.BaseUrl}/api/merchant/order", request, cancellationToken, headers: headers).ConfigureAwait(false);
    }

    public async Task<EasyPosTokenResponseDto> GetEasyPosTokenAsync(EasyPosTokenRequestDto request, CancellationToken cancellationToken)
    {
        return await _httpClientFactory.PostAsJsonAsync<EasyPosTokenResponseDto>(
            $"{_easyPosSetting.BaseUrl}/api/merchant/oauth/token", request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EasyPosResponseDto> GetPosCompanyStoreMenusAsync(EasyPosTokenRequestDto request, CancellationToken cancellationToken)
    {
        var authorization = await GetEasyPosTokenAsync(request, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Getting the store pos token");
        
        if (authorization == null || string.IsNullOrEmpty(authorization.Data) || !authorization.Success)
        {
            throw new Exception("Failed to get token");
        }
        
        return await _httpClientFactory.GetAsync<EasyPosResponseDto>(
            requestUrl: $"{_easyPosSetting.BaseUrl}/api/merchant/resource", headers: new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {authorization.Data}"}
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<EasyPosMerchantResponseDto> GetPosCompanyStoreMessageAsync(EasyPosTokenRequestDto request, CancellationToken cancellationToken)
    {
        var authorization = await GetEasyPosTokenAsync(request, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Getting the store pos token");

        if (authorization == null || string.IsNullOrEmpty(authorization.Data) || !authorization.Success)
        {
            throw new Exception("Failed to get token");
        }
        
        return await _httpClientFactory.GetAsync<EasyPosMerchantResponseDto>(
            requestUrl: $"{_easyPosSetting.BaseUrl}/api/merchant", headers: new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {authorization.Data}"}
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<PlaceOrderToEasyPosResponseDto> PlaceOrderAsync(PlaceOrderToEasyPosRequestDto request, string token, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        return await _httpClientFactory.PostAsJsonAsync<PlaceOrderToEasyPosResponseDto>(
            $"{_easyPosSetting.BaseUrl}/api/merchant/order", request, cancellationToken,
            headers: new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {token}" }
            }, timeout: timeout).ConfigureAwait(false);
    }

    public (string Authorization, string MerchantId, string CompanyId, string MerchantStaffId) GetRestaurantAuthHeaders(string restaurantName)
    {
        return restaurantName switch
        {
            RestaurantStore.MoonHouse =>
                (_easyPosSetting.MoonHouseAuthorization, _easyPosSetting.MoonHouseMerchantId, _easyPosSetting.MoonHouseCompanyId, _easyPosSetting.MoonHouseMerchantStaffId),
            _ => throw new NotSupportedException(restaurantName)
        };
    }
}