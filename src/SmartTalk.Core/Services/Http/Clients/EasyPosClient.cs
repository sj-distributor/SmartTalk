using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.EasyPos;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.Http.Clients;

public interface IEasyPosClient : IScopedDependency
{
    Task<EasyPosResponseDto> GetEasyPosRestaurantMenusAsync(PhoneOrderRestaurant restaurant, CancellationToken cancellationToken);

    Task<GetOrderResponse> GetOrderAsync(long id, PhoneOrderRestaurant restaurant, CancellationToken cancellationToken);
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
    
    public async Task<EasyPosResponseDto> GetEasyPosRestaurantMenusAsync(PhoneOrderRestaurant restaurant, CancellationToken cancellationToken)
    {
        var (authorization, merchantId, companyId, merchantStaffId) = GetRestaurantAuthHeaders(restaurant);
        
        return await _httpClientFactory.GetAsync<EasyPosResponseDto>(
            requestUrl: $"{_easyPosSetting.BaseUrl}/api/merchant/resource", headers: new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {authorization}"},
                { "MerchantId", merchantId },
                { "CompanyId", companyId },
                { "MerchantStaffId", merchantStaffId }
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<GetOrderResponse> GetOrderAsync(long id, PhoneOrderRestaurant restaurant, CancellationToken cancellationToken)
    {
        var (authorization, merchantId, companyId, merchantStaffId) = GetRestaurantAuthHeaders(restaurant);
        
        return await _httpClientFactory.GetAsync<GetOrderResponse>(
            requestUrl: $"{_easyPosSetting.BaseUrl}/api/merchant/order?id={ id }", headers: new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {authorization}"},
                { "MerchantId", merchantId },
                { "CompanyId", companyId },
                { "MerchantStaffId", merchantStaffId }
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public (string Authorization, string MerchantId, string CompanyId, string MerchantStaffId) GetRestaurantAuthHeaders(PhoneOrderRestaurant restaurant)
    {
        return restaurant switch
        {
            PhoneOrderRestaurant.MoonHouse =>
                (_easyPosSetting.Authorizations[0], _easyPosSetting.MerchantIds[0], _easyPosSetting.CompanyIds[0], _easyPosSetting.MerchantStaffIds[0]),
            PhoneOrderRestaurant.JiangNanChun =>
                (_easyPosSetting.Authorizations[1], _easyPosSetting.MerchantIds[1], _easyPosSetting.CompanyIds[1], _easyPosSetting.MerchantStaffIds[1]),
            PhoneOrderRestaurant.XiangTanRenJia =>
                (_easyPosSetting.Authorizations[2], _easyPosSetting.MerchantIds[2], _easyPosSetting.CompanyIds[2], _easyPosSetting.MerchantStaffIds[2]),
            _ => throw new NotSupportedException(nameof(restaurant))
        };
    }
}