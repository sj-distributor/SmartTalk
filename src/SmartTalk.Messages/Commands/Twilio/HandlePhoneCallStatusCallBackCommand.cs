using Mediator.Net.Contracts;
using SmartTalk.Messages.DTO.Communication;
using SmartTalk.Messages.Enums.Communication.PhoneCall;

namespace SmartTalk.Messages.Commands.Twilio;

public class HandlePhoneCallStatusCallBackCommand : ICommand
{
    public PhoneCallProvider Provider { get; set; }
    
    public ICommunicationCallBackDto CallBackMessage { get; set; }
}