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
    
    Task<GetCustomerNumbersByNameResponseDto> GetCustomerNumbersByNameAsync(GetCustomerNumbersByNameRequestDto request, CancellationToken cancellationToken); 
}

public class SalesClient : ISalesClient
{
    private readonly SalesSetting _salesSetting;
    private readonly Dictionary<string, string> _headers;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;

    public SalesClient(SalesSetting salesSetting, ISmartTalkHttpClientFactory httpClientFactory)
    {
        _salesSetting = salesSetting;
        _httpClientFactory = httpClientFactory;
        
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

    public async Task<GetCustomerNumbersByNameResponseDto> GetCustomerNumbersByNameAsync(GetCustomerNumbersByNameRequestDto request, CancellationToken cancellationToken)
    {
        var url = $"{_salesSetting.BaseUrl}/api/SalesOrder/GetCustomerNumbersByName?customerName={Uri.EscapeDataString(request.CustomerName)}";
        
        return await _httpClientFactory.GetAsync<GetCustomerNumbersByNameResponseDto>(url, headers: _headers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}