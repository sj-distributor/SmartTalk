using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.Hr;

public class AddHrInterviewQuestionsCommand : ICommand
{
    public List<string> Questions { get; set; }
}