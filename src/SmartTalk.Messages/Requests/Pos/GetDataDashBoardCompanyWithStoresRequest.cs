using Mediator.Net.Contracts;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

[SmartTalkAuthorize(Permissions = new[] { SecurityStore.Permissions.CanViewDataDashboard })]
public class GetDataDashBoardCompanyWithStoresRequest: HasServiceProviderId, IRequest
{
    public int? PageIndex { get; set; }
    
    public int? PageSize { get; set; }
    
    public string Keyword { get; set; }
}

public class GetDataDashBoardCompanyWithStoresResponse : SmartTalkResponse<GetDataDashBoardCompanyWithStoresResponseData>;

public class GetDataDashBoardCompanyWithStoresResponseData
{
    public int Count { get; set; }
    
    public List<GetCompanyWithStoresData> Data { get; set; }
}