using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetPosStoresRequest : PosHasServiceId, IRequest
{
    public int? CompanyId { get; set; }
    
    public string Keyword { get; set; }
    
    public bool AuthorizedFilter { get; set; } = true;
    
    public bool IsNormalSort { get; set; } = false;
}

public class GetPosStoresResponse : SmartTalkResponse<List<PosCompanyStoreDto>>;