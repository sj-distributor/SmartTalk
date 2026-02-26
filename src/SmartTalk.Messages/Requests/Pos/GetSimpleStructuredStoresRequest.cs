using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetSimpleStructuredStoresRequest : HasServiceProviderId, IRequest
{
    public List<int> AgentIds { get; set; }

    public List<TaskType> TaskTypes { get; set; }
}

public class GetSimpleStructuredStoresResponse : SmartTalkResponse<GetSimpleStructuredStoresResponseData>
{
}

public class GetSimpleStructuredStoresResponseData
{
    public List<SimpleStructuredStoreDto> StructuredStores { get; set; }
    
    public int UnreviewTotalCount => StructuredStores.Sum(x => x.UnreviewTotalCount);

    public int? WaitingProcessingEventCount { get; set; }
}