namespace SmartTalk.Core.Services.Communication.Exceptions;

public class MissingThirdPartyProviderException : Exception
{
    public MissingThirdPartyProviderException() : base("Missing third party communication provider!")
    {
    }
}