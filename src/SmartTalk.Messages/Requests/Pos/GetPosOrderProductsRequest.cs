using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetPosOrderProductsRequest : IRequest
{
    public int StoreId { get; set; }
    
    public List<string> ProductIds { get; set; }
}

public class GetPosOrderProductsResponse : SmartTalkResponse<List<GetPosOrderProductsResponseData>>;

public class GetPosOrderProductsResponseData
{
    public PosMenuDto Menu { get; set; }
    
    public PosCategoryDto Category { get; set; }
    
    public PosProductDto Product { get; set; }
}