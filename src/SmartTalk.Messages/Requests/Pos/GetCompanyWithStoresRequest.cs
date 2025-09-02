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

public class GetPosCompanyWithStoresResponse : SmartTalkResponse<GetPosCompanyWithStoresResponseData>;

public class GetPosCompanyWithStoresResponseData
{
    public int Count { get; set; }
    
    public List<GetPosCompanyWithStoresData> Data { get; set; }
}

public class GetPosCompanyWithStoresData
{
    public int Count { get; set; }
    
    public PosCompanyDto Company { get; set; }
    
    public List<PosCompanyStoreDto> Stores { get; set; }
}
