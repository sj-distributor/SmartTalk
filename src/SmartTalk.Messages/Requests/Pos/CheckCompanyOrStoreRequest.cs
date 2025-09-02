using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class CheckCompanyOrStoreRequest : IRequest
{
    public int? ComapnyId { get; set; }
    
    public int? StoreId { get; set; }
}

public class CheckCompanyOrStoreResponse : SmartTalkResponse<CheckCompanyOrStoreResponseData>;

public class CheckCompanyOrStoreResponseData
{
    public bool IsAllow { get; set; }
    
    public string Message { get; set; }
}