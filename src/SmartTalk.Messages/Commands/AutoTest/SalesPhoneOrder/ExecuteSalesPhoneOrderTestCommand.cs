using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.AutoTest.SalesPhoneOrder;

public class ExecuteSalesPhoneOrderTestCommand : ICommand
{
    public int TaskId { get; set; }
}