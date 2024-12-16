using SmartTalk.Core.Ioc;
using SmartTalk.Core.Utils;
using SmartTalk.Core.Services.Communication.Providers;
using SmartTalk.Core.Settings.Communication.PhoneCall;
using SmartTalk.Messages.Enums.Communication.PhoneCall;

namespace SmartTalk.Core.Services.Communication;

public interface ICommunicationProviderSwitcher : IScopedDependency
{
    IPhoneCallProvider PhoneCallProvider(PhoneCallProvider? phoneCallProvider = null, bool? random = true);
}

public class CommunicationProviderSwitcher : ICommunicationProviderSwitcher
{
    private readonly IEnumerable<IPhoneCallProvider> _phoneCallProviders;
    private readonly PhoneCallProvidersSetting _phoneCallProvidersSetting;

    public CommunicationProviderSwitcher(
        IEnumerable<IPhoneCallProvider> phoneCallProviders,
        PhoneCallProvidersSetting phoneCallProvidersSetting)
    {
        _phoneCallProviders = phoneCallProviders;
        _phoneCallProvidersSetting = phoneCallProvidersSetting;
    }
    
    public IPhoneCallProvider PhoneCallProvider(PhoneCallProvider? phoneCallProvider = null, bool? random = true)
    {
        phoneCallProvider = ProvidersPool.GetProvider(_phoneCallProvidersSetting.Value.ToList(), phoneCallProvider, random);
        
        return _phoneCallProviders.FirstOrDefault(x => x.PhoneCallProvider == phoneCallProvider);
    }
}