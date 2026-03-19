using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.DynamicInterface;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.DynamicInterface;

public class GetDynamicInterfaceTreeRequest : IRequest
{
    public string Keyword { get; set; }
}

public class GetDynamicInterfaceTreeResponse: SmartTalkResponse<GetDynamicInterfaceTreeResponseData>
{
}

public class GetDynamicInterfaceTreeResponseData
{
    public List<DynamicInterfaceTreeNodeDto> TreeNodes { get; set; }
}