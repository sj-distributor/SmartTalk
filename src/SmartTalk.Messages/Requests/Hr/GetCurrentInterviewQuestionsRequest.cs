using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Hr;
using SmartTalk.Messages.Enums.Hr;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Hr;

public class GetCurrentInterviewQuestionsRequest : IRequest
{
    public HrInterviewQuestionSection? Section { get; set; }
}

public class GetCurrentInterviewQuestionsResponse : SmartTalkResponse<GetCurrentInterviewQuestionsResponseData>;

public class GetCurrentInterviewQuestionsResponseData
{
    public List<HrInterviewQuestionDto> Questions { get; set; }
}