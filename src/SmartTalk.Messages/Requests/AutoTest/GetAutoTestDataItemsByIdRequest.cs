using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AutoTest;

public class GetAutoTestDataItemsByIdRequest : IRequest
{
    public int DataSetId { get; set; }

    public int? Page { get; set; }

    public int? PageSize { get; set; }
}

public class GetAutoTestDataItemsByIdResponse : SmartTalkResponse
{
    public int Count { get; set; }
    
    public List<AutoTestDataItemDto> Data { get; set; }
}