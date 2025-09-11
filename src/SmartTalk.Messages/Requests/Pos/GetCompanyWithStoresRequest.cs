using Mediator.Net.Contracts;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

[SmartTalkAuthorize(Permissions = new[] { SecurityStore.Permissions.CanViewBusinessManagement })]
public class GetCompanyWithStoresRequest : HasServiceProviderId, IRequest
{
    public int? PageIndex { get; set; }
    
    public int? PageSize { get; set; }
    
    public string Keyword { get; set; }
}

public class GetCompanyWithStoresResponse : SmartTalkResponse<GetCompanyWithStoresResponseData>;

public class GetCompanyWithStoresResponseData
{
    public int Count { get; set; }
    
    public List<GetCompanyWithStoresData> Data { get; set; }
}

public class GetCompanyWithStoresData
{
    public int Count { get; set; }
    
    public CompanyDto Company { get; set; }
    
    public List<CompanyStoreDto> Stores { get; set; }
}
