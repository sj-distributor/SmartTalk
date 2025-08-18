using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class CheckPosCompanyOrStoreRequest : IRequest
{
    public int? ComapnyId { get; set; }
    
    public int? StoreId { get; set; }
}

public class CheckPosCompanyOrStoreResponse : SmartTalkResponse<CheckPosCompanyOrStoreResponseData>;

public class CheckPosCompanyOrStoreResponseData
{
    public bool IsAllow { get; set; }
    
    public string Message { get; set; }
}