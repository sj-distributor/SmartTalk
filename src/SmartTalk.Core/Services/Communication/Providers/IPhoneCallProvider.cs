using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.Twilio;
using SmartTalk.Messages.Enums.Communication.PhoneCall;

namespace SmartTalk.Core.Services.Communication.Providers;

public interface IPhoneCallProvider : IScopedDependency
{
    public PhoneCallProvider PhoneCallProvider { get; }

    Task HandlePhoneCallStatusCallbackAsync(HandlePhoneCallStatusCallBackCommand command, CancellationToken cancellationToken);
}