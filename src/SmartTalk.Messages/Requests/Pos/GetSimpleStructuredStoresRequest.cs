using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetSimpleStructuredStoresRequest : IRequest
{
}

public class GetSimpleStructuredStoresResponse : SmartTalkResponse<GetSimpleStructuredStoresResponseData>
{
}

public class GetSimpleStructuredStoresResponseData
{
    public List<SimpleStructuredStoreDto> StructuredStores { get; set; }
    
    public int UnreviewTotalCount => StructuredStores.Sum(x => x.UnreviewTotalCount);
}