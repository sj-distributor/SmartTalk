using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.Pos;

public class RetryCloudPrintingCommand : ICommand
{
    public Guid Id { get; set; }

    public int Count { get; set; }
}