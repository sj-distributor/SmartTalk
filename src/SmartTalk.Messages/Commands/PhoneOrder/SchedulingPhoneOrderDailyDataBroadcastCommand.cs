using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.PhoneOrder;

public class SchedulingPhoneOrderDailyDataBroadcastCommand : ICommand
{
    public string RobotUrl { get; set; }
}