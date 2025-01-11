using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.PhoneCall;

public class SchedulingPhoneCallDailyDataBroadcastCommand : ICommand
{
    public List<string> RobotUrl { get; set; }
}