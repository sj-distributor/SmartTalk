using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.Hr;

namespace SmartTalk.Messages.Commands.Hr;

public class AddHrInterviewQuestionsCommand : ICommand
{
    public HrInterviewQuestionSection Section { get; set; }
    
    public List<string> Questions { get; set; }
}