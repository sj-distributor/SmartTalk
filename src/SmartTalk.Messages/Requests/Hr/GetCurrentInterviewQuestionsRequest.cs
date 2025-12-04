using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.Hr;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Hr;

public class GetCurrentInterviewQuestionsRequest : IRequest
{
    public HrInterviewQuestionSection? Section { get; set; }
}