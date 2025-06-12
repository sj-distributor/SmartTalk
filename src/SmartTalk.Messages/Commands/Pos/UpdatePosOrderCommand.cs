using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.Pos;

public class UpdatePosOrderCommand : ICommand
{
    public string OrderId { get; set; }
}