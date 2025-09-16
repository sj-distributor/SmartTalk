using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.Pos;

public class UpdatePosOrderCommand : ICommand
{
    public long OrderId { get; set; }
}