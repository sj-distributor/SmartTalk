using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Dto.Linphone;

namespace SmartTalk.Messages.Requests.Linphone;

public class GetLinphoneHistoryRequest : IRequest
{
    public List<int> AgentId { get; set; }

    public string RestaurantName { get; set; }

    public int PageSize { get; set; } = 10;

    public int PageIndex { get; set; } = 1;
}

public class GetLinphoneHistoryResponse : SmartTalkResponse<GetLinphoneHistoryDto>
{
}

public class GetLinphoneHistoryDto
{
    public List<LinphoneHistoryDto> linphoneRecords { get; set; }

    public int Count { get; set; }
}