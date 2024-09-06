using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.Http.Clients;

public interface IEasyPosClient : IScopedDependency
{
    Task<EasyPosResponseDto> GetEasyPosRestaurantMenusAsync(PhoneOrderRestaurant restaurant, CancellationToken cancellationToken);
}

public class EasyPosClient : IEasyPosClient
{
    private readonly ISmartiesHttpClientFactory _httpClientFactory;

    public EasyPosClient(ISmartiesHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }
    
    public async Task<EasyPosResponseDto> GetEasyPosRestaurantMenusAsync(PhoneOrderRestaurant restaurant, CancellationToken cancellationToken)
    {
        var (authorization, merchantId, companyId, merchantStaffId) = GetRestaurantAuthHeaders(restaurant);
        
        return await _httpClientFactory.GetAsync<EasyPosResponseDto>(
            requestUrl: "https://roosterpos-test-api.proton-system.com/api/merchant/resource", headers: new Dictionary<string, string>
            {
                { "Authorization", authorization},
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
            ("Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6Ijg5NjM2MDM1ODkwMzkxMDkiLCJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9zeXN0ZW0iOiJDb21wYW55QWNjb3VudCIsIm5iZiI6MTcyNTQzMTkxMiwiZXhwIjoxNzg4NTAzOTEyLCJpc3MiOiJodHRwczovL2Vhc3lwb3MuY29tIiwiYXVkIjoic2luZ2xlLXBvcyJ9.zsExvb8cZl1pC4VBS3I5j72ha0ck3RRwZzyDSYfZwc8",
                "9224998680331269", "9224980557202437", "9224998683214853"),
            PhoneOrderRestaurant.JiangNanChun =>
                ("Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6Ijg5NjM2MDM1ODkwMzkxMDkiLCJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9zeXN0ZW0iOiJDb21wYW55QWNjb3VudCIsIm5iZiI6MTcyNTQzMjgzNywiZXhwIjoxNzg4NTA0ODM3LCJpc3MiOiJodHRwczovL2Vhc3lwb3MuY29tIiwiYXVkIjoic2luZ2xlLXBvcyJ9.zRgxr5msw5RC8t8soCQJCjnzhNy3PQgr3WeUsV4usUM",
                    "9236665971508229", "9236657812603909", "9236665975047173"),
            PhoneOrderRestaurant.XiangTanRenJia =>
                ("Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6Ijg5NjM2MDM1ODkwMzkxMDkiLCJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9zeXN0ZW0iOiJDb21wYW55QWNjb3VudCIsIm5iZiI6MTcyNTQzMjk0NywiZXhwIjoxNzg4NTA0OTQ3LCJpc3MiOiJodHRwczovL2Vhc3lwb3MuY29tIiwiYXVkIjoic2luZ2xlLXBvcyJ9.oHdrFrB8deaaoEas5jBG_BNgcvg1xKklfi4TY_v67UM",
                    "9078939242529797", "9078928332948485", "9078939246134277"),
            _ => throw new NotSupportedException(nameof(restaurant))
        };
    }
}