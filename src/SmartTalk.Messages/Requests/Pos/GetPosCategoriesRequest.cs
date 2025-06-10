using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetPosCategoriesRequest : IRequest
{
    public int MenuId { get; set; }
}

public class GetPosCategoriesResponse : SmartTalkResponse<List<PosCategoryDto>>;