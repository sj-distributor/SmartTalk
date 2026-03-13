using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.Twilio;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using RecordingResource = Twilio.Rest.Api.V2010.Account.Call.RecordingResource;

namespace SmartTalk.Core.Services.Twilio;

public interface ITwilioService : IScopedDependency
{
    Task UpdateCallTwimlAsync(string callSid, string twiml);

    Task CompleteCallAsync(string callSid);

    Task<TwilioCallInfo> FetchCallAsync(string callSid);

    Task CreateRecordingAsync(string callSid, Uri recordingStatusCallback);
}

public record TwilioCallInfo(string From, string To, DateTimeOffset? StartTime);

public class TwilioService : ITwilioService
{
    private readonly TwilioSettings _twilioSettings;

    public TwilioService(TwilioSettings twilioSettings)
    {
        _twilioSettings = twilioSettings;
    }

    public async Task UpdateCallTwimlAsync(string callSid, string twiml)
    {
        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);

        await CallResource.UpdateAsync(pathSid: callSid, twiml: twiml);
    }

    public async Task CompleteCallAsync(string callSid)
    {
        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);

        await CallResource.UpdateAsync(pathSid: callSid, status: CallResource.UpdateStatusEnum.Completed);
    }

    public async Task<TwilioCallInfo> FetchCallAsync(string callSid)
    {
        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);

        var call = await CallResource.FetchAsync(pathSid: callSid);

        return new TwilioCallInfo(call?.From, call?.To, call?.StartTime);
    }

    public async Task CreateRecordingAsync(string callSid, Uri recordingStatusCallback)
    {
        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);

        await RecordingResource.CreateAsync(
            pathCallSid: callSid,
            recordingStatusCallbackMethod: global::Twilio.Http.HttpMethod.Post,
            recordingStatusCallback: recordingStatusCallback);
    }
}
