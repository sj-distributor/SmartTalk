using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.AutoTest;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AutoTest;

public class AutoTestRunningCommand : ICommand
{
    public AutoTestRunningType TestRunningType { get; set; }
    
    public int ScenarioId { get; set; }
}

public class AutoTestRunningResponse : SmartTalkResponse<string>
{
}