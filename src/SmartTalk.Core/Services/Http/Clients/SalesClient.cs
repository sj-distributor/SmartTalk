using Serilog;
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

    Task<QueryGoodsStatusResponseDto> QueryGoodsStatusAsync(QueryGoodsStatusRequestDto request, CancellationToken cancellationToken);

    Task<GetOrderInformationByCustomerIdResponseDto> GetOrderInformationByCustomerIdAsync(GetOrderInformationByCustomerIdRequestDto request, CancellationToken cancellationToken);
    
    Task<GetCustomerAiQuotationResponseDto> GetCustomerAiQuotationAsync(GetCustomerAiQuotationRequestDto request, CancellationToken cancellationToken);
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
        var customerNumbers = NormalizeCustomerNumbers(request?.CustomerNumbers);
        if (customerNumbers.Count == 0)
            throw new ArgumentException("CustomerNumbers cannot be null or empty.");

        return await _httpClientFactory.PostAsJsonAsync<GetAskInfoDetailListByCustomerResponseDto>(
                $"{_salesSetting.BaseUrl}/api/SalesOrder/GetAskInfoDetailListByCustomer",
                new GetAskInfoDetailListByCustomerRequestDto { CustomerNumbers = customerNumbers },
                headers: _headers,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<GetOrderHistoryByCustomerResponseDto> GetOrderHistoryByCustomerAsync(GetOrderHistoryByCustomerRequestDto request, CancellationToken cancellationToken)
    {
        var customerNumbers = ResolveOrderHistoryCustomerNumbers(request);
        if (customerNumbers.Count == 0)
            throw new ArgumentException("CustomerNumbers cannot be null or empty.");

        return await _httpClientFactory.PostAsJsonAsync<GetOrderHistoryByCustomerResponseDto>(
                $"{_salesSetting.BaseUrl}/api/SalesOrder/GetOrderHistoryByCustomer",
                new GetOrderHistoryByCustomerRequestDto { CustomerNumbers = customerNumbers },
                headers: _headers,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static List<string> ResolveOrderHistoryCustomerNumbers(GetOrderHistoryByCustomerRequestDto request)
    {
        var customerNumbers = request?.CustomerNumbers ?? Enumerable.Empty<string>();

        if (!string.IsNullOrWhiteSpace(request?.CustomerNumber))
            customerNumbers = customerNumbers.Append(request.CustomerNumber);

        return NormalizeCustomerNumbers(customerNumbers);
    }

    private static List<string> NormalizeCustomerNumbers(IEnumerable<string> customerNumbers)
    {
        return (customerNumbers ?? Enumerable.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    public async Task<GetCustomerLevel5HabitResponseDto> GetCustomerLevel5HabitAsync(GetCustomerLevel5HabitRequstDto request, CancellationToken cancellationToken)
    {
        var header = new Dictionary<string, string>
        {
            { "X-API-KEY", _salesCustomerHabitSetting.ApiKey }
        };

        return await _httpClientFactory.PostAsJsonAsync<GetCustomerLevel5HabitResponseDto>($"{_salesCustomerHabitSetting.BaseUrl}/api/CustomerInfo/QueryHistoryCustomerLevel5Habit", request, headers: header, cancellationToken: cancellationToken).ConfigureAwait(false);
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

    public async Task<DeleteAiOrderResponseDto> DeleteAiOrderAsync(DeleteAiOrderRequestDto request, CancellationToken cancellationToken)
    {
        return await _httpClientFactory.PostAsJsonAsync<DeleteAiOrderResponseDto>($"{_salesSetting.BaseUrl}/api/SalesOrder/DeleteAiOrder", request, headers: _headers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<GetAiOrderItemsByDeliveryDateResponseDto> GetAiOrderItemsByDeliveryDateAsync(GetAiOrderItemsByDeliveryDateRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerNumber))
            Log.Information("CustomerNumbers cannot be null or empty.");

        var deliveryDate = request.DeliveryDate.ToString("yyyy-MM-dd");

        var url = $"{_salesSetting.BaseUrl}/api/SalesOrder/GetAiOrderItemsByDeliveryDate" + $"?customerNumber={Uri.EscapeDataString(request.CustomerNumber)}"
                  + $"&deliveryDate={deliveryDate}" + $"&includePrintedQuantity={request.IncludePrintedQuantity}";

        return await _httpClientFactory.GetAsync<GetAiOrderItemsByDeliveryDateResponseDto>(url, headers: _headers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<QueryGoodsStatusResponseDto> QueryGoodsStatusAsync(QueryGoodsStatusRequestDto request, CancellationToken cancellationToken)
    {
        if (request?.List == null || request.List.Count == 0)
            throw new ArgumentException("List cannot be null or empty.");

        var url = $"{_salesSetting.BaseUrl}/api/GoodsStatus/QueryGoodsStatus";

        var response = await _httpClientFactory.PostAsJsonAsync<QueryGoodsStatusResponseDto>(url, request, headers: _headers, cancellationToken: cancellationToken).ConfigureAwait(false);

        return response;
    }

    public async Task<GetOrderInformationByCustomerIdResponseDto> GetOrderInformationByCustomerIdAsync(GetOrderInformationByCustomerIdRequestDto request, CancellationToken cancellationToken)
    {
        if (request?.CustomerIds == null || request.CustomerIds.Count == 0)
            throw new ArgumentException("CustomerIds cannot be null or empty.");

        var url = $"{_salesSetting.BaseUrl}/api/SmartalkAI/GetOrderInformationByCustomerId";

        return await _httpClientFactory.PostAsJsonAsync<GetOrderInformationByCustomerIdResponseDto>(url, request, headers: _headers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<GetCustomerAiQuotationResponseDto> GetCustomerAiQuotationAsync(GetCustomerAiQuotationRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.CustomerId))
            throw new ArgumentException("CustomerId cannot be null or empty.");

        if (request.MaterialIdList == null || request.MaterialIdList.Count == 0)
            throw new ArgumentException("MaterialIdList cannot be null or empty.");

        var url = $"{_salesSetting.BaseUrl}/api/CustomerInfo/GetCustomerAiQuotation";

        return await _httpClientFactory.PostAsJsonAsync<GetCustomerAiQuotationResponseDto>(url, request, headers: _headers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
