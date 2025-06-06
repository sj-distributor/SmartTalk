using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetPosCategoryRequest : IRequest
{
    public int Id { get; set; }
}

public class GetPosCategoryResponse : SmartTalkResponse<List<PosCategoryDto>>
{
}