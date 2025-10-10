using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetPosProductsRequest : IRequest
{
    public int? CategoryId { get; set; }
    
    public string KeyWord { get; set; }
    
    public bool? IsActive { get; set; }
    
    public int? StoreId { get; set; }
}

public class GetPosProductsResponse : SmartTalkResponse<List<PosProductDto>>;