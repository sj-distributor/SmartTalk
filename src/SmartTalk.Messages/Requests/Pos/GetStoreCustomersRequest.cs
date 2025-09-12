using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetStoreCustomersRequest : IRequest
{
    public int? PageIndex { get; set; }
    
    public int? PageSize { get; set; }
    
    public int StoreId { get; set; }
    
    public string Phone { get; set; }
}

public class GetStoreCustomersResponse : SmartTalkResponse<GetPosCustomerInfoResponseData>;

public class GetPosCustomerInfoResponseData
{
    public int Count { get; set; }
    
    public List<StoreCustomerDto> Customers { get; set; }
}