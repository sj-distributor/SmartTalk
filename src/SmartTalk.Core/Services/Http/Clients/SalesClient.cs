using System.Text;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.Sales;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Core.Services.Http.Clients;

public interface ISalesClient : IScopedDependency
{
    Task<GetAskInfoDetailListByCustomerResponseDto> GetAskInfoDetailListByCustomerAsync(GetAskInfoDetailListByCustomerRequestDto request, CancellationToken cancellationToken);
    
    Task<GetOrderHistoryByCustomerResponseDto> GetOrderHistoryByCustomerAsync(GetOrderHistoryByCustomerRequestDto request, CancellationToken cancellationToken);
    
    Task<SalesResponseDto> GenerateAiOrdersAsync(GenerateAiOrdersRequestDto request, CancellationToken cancellationToken);
    
    Task<GetOrderArrivalTimeResponseDto> GetOrderArrivalTimeAsync(GetOrderArrivalTimeRequestDto request, CancellationToken cancellationToken);
    
    Task<GetCustomerNumbersByNameResponseDto> GetCustomerNumbersByNameAsync(GetCustomerNumbersByNameRequestDto request, CancellationToken cancellationToken); 

    Task<GetCustomerLevel5HabitResponseDto> GetCustomerLevel5HabitAsync(GetCustomerLevel5HabitRequstDto request, CancellationToken cancellationToken);
    
    Task<DeleteAiOrderResponseDto> DeleteAiOrderAsync(DeleteAiOrderRequestDto request, CancellationToken cancellationToken);
    
    Task<GetAiOrderItemsByDeliveryDateResponseDto> GetAiOrderItemsByDeliveryDateAsync(GetAiOrderItemsByDeliveryDateRequestDto request, CancellationToken cancellationToken);
}

public class SalesClient : ISalesClient
{
    private readonly SalesSetting _salesSetting;
    private readonly Dictionary<string, string> _headers;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly SalesOrderArrivalSetting _salesOrderArrivalSetting;
    private readonly SalesCustomerHabitSetting _salesCustomerHabitSetting;

    public SalesClient(SalesSetting salesSetting, ISmartTalkHttpClientFactory httpClientFactory, SalesOrderArrivalSetting salesOrderArrivalSetting, SalesCustomerHabitSetting salesCustomerHabitSetting)
    {
        _salesSetting = salesSetting;
        _httpClientFactory = httpClientFactory;
        _salesOrderArrivalSetting = salesOrderArrivalSetting;
        _salesCustomerHabitSetting = salesCustomerHabitSetting;

        _headers = new Dictionary<string, string>
        {
            { "X-API-KEY", _salesSetting.ApiKey }
        };
    }

    public async Task<GetAskInfoDetailListByCustomerResponseDto> GetAskInfoDetailListByCustomerAsync(GetAskInfoDetailListByCustomerRequestDto request, CancellationToken cancellationToken)
    {
        if (request.CustomerNumbers == null || request.CustomerNumbers.Count == 0)
            throw new ArgumentException("CustomerNumbers cannot be null or empty.");
        
        var queryString = new StringBuilder("?");
        
        foreach (var customerNumber in request.CustomerNumbers)
        {
            queryString.Append("CustomerNumbers=").Append(Uri.EscapeDataString(customerNumber)).Append('&');
        }
        
        var url = $"{_salesSetting.BaseUrl}/api/SalesOrder/GetAskInfoDetailListByCustomer" + queryString.ToString().TrimEnd('&');

        return await _httpClientFactory.GetAsync<GetAskInfoDetailListByCustomerResponseDto>(url, headers: _headers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<GetOrderHistoryByCustomerResponseDto> GetOrderHistoryByCustomerAsync(GetOrderHistoryByCustomerRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.CustomerNumber))
            throw new ArgumentException("CustomerNumbers cannot be null or empty.");
        
        var url = $"{_salesSetting.BaseUrl}/api/SalesOrder/GetOrderHistoryByCustomer?customerNumber={request.CustomerNumber}";
        
        return await _httpClientFactory.GetAsync<GetOrderHistoryByCustomerResponseDto>(url, headers: _headers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<SalesResponseDto> GenerateAiOrdersAsync(GenerateAiOrdersRequestDto request, CancellationToken cancellationToken)
    {
        return await _httpClientFactory.PostAsJsonAsync<SalesResponseDto>($"{_salesSetting.BaseUrl}/api/SalesOrder/GenerateAiOrders", request, headers: _headers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<GetOrderArrivalTimeResponseDto> GetOrderArrivalTimeAsync(GetOrderArrivalTimeRequestDto request, CancellationToken cancellationToken)
    {
        var header = new Dictionary<string, string>
        {
            { "apikey", _salesOrderArrivalSetting.ApiKey },
            { "Organizationid", _salesOrderArrivalSetting.Organizationid },
        };
        
        if (request.CustomerIds == null || request.CustomerIds.Count == 0)
            throw new ArgumentException("CustomerIds cannot be null or empty.");

        return await _httpClientFactory.PostAsJsonAsync<GetOrderArrivalTimeResponseDto>($"{_salesOrderArrivalSetting.BaseUrl}/api/order/getOrderArrivalTime", request, headers: header, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<GetCustomerNumbersByNameResponseDto> GetCustomerNumbersByNameAsync(GetCustomerNumbersByNameRequestDto request, CancellationToken cancellationToken)
    {
        var url = $"{_salesSetting.BaseUrl}/api/SalesOrder/GetCustomerNumbersByName?customerName={Uri.EscapeDataString(request.CustomerName)}";

        return await _httpClientFactory.GetAsync<GetCustomerNumbersByNameResponseDto>(url, headers: _headers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<GetCustomerLevel5HabitResponseDto> GetCustomerLevel5HabitAsync(GetCustomerLevel5HabitRequstDto request, CancellationToken cancellationToken)
    {
        var header = new Dictionary<string, string>
        {
            { "X-API-KEY", _salesCustomerHabitSetting.ApiKey }
        };
        
        return await _httpClientFactory.PostAsJsonAsync<GetCustomerLevel5HabitResponseDto>($"{_salesCustomerHabitSetting.BaseUrl}/api/CustomerInfo/QueryHistoryCustomerLevel5Habit", request, headers: header, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeleteAiOrderResponseDto> DeleteAiOrderAsync(DeleteAiOrderRequestDto request, CancellationToken cancellationToken)
    {
        return await _httpClientFactory.PostAsJsonAsync<DeleteAiOrderResponseDto>($"{_salesSetting.BaseUrl}/api/SalesOrder/DeleteAiOrder", request, headers: _headers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<GetAiOrderItemsByDeliveryDateResponseDto> GetAiOrderItemsByDeliveryDateAsync(GetAiOrderItemsByDeliveryDateRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerNumber)) throw new ArgumentException("CustomerNumber cannot be null or empty.");

        var deliveryDate = request.DeliveryDate.ToString("yyyy-MM-dd");

        var url = $"{_salesSetting.BaseUrl}/api/SalesOrder/GetAiOrderItemsByDeliveryDate" + $"?customerNumber={Uri.EscapeDataString(request.CustomerNumber)}" + $"&deliveryDate={deliveryDate}";

        return await _httpClientFactory.GetAsync<GetAiOrderItemsByDeliveryDateResponseDto>(url, headers: _headers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}