using System.Text;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Settings.Twilio;
using SmartTalk.Messages.Requests.Twilio;
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

    Task<MigrateIncomingPhoneNumberResponse> MigrateIncomingPhoneNumberAsync(MigrateIncomingPhoneNumberRequest request, CancellationToken cancellationToken = default);
}

public record TwilioCallInfo(string From, string To, DateTimeOffset? StartTime);

public class TwilioService : ITwilioService
{
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly TwilioSettings _twilioSettings;

    public TwilioService(TwilioSettings twilioSettings, ISmartTalkHttpClientFactory httpClientFactory)
    {
        _twilioSettings = twilioSettings;
        _httpClientFactory = httpClientFactory;
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

    public async Task<MigrateIncomingPhoneNumberResponse> MigrateIncomingPhoneNumberAsync(MigrateIncomingPhoneNumberRequest request, CancellationToken cancellationToken = default)
    {
        var losingAccountSid = request.LosingAccountSid;
        var authToken = request.LosingAccountAuthToken;
        var gainingAccountSid = request.GainingAccountSid;

        var requestUrl =
            $"https://api.twilio.com/2010-04-01/Accounts/{Uri.EscapeDataString(losingAccountSid)}/IncomingPhoneNumbers/{Uri.EscapeDataString(request.PhoneNumberSid.Trim())}.json";
        var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{losingAccountSid}:{authToken}"));

        var headers = new Dictionary<string, string>
        {
            { "Accept", "application/json" },
            { "Authorization", $"Basic {authValue}" }
        };

        var formData = new Dictionary<string, string>
        {
            { "AccountSid", gainingAccountSid }
        };

        if (!string.IsNullOrWhiteSpace(request.BundleSid))
            formData["BundleSid"] = request.BundleSid.Trim();
        if (!string.IsNullOrWhiteSpace(request.AddressSid))
            formData["AddressSid"] = request.AddressSid.Trim();

        var content = new FormUrlEncodedContent(formData);

        var response = await _httpClientFactory
            .PostAsync<MigrateIncomingPhoneNumberResponse>(requestUrl, content, cancellationToken, headers: headers, isNeedToReadErrorContent: true).ConfigureAwait(false);

        if (response == null)
            throw new InvalidOperationException("Twilio number migration failed: empty response.");

        if (!string.IsNullOrWhiteSpace(response.Sid))
        {
            Log.Information(
                "Twilio number migration succeeded. PhoneNumberSid: {PhoneNumberSid}, LosingAccountSid: {LosingAccountSid}, GainingAccountSid: {GainingAccountSid}",
                request.PhoneNumberSid,
                losingAccountSid,
                gainingAccountSid);
            return response;
        }

        Log.Warning(
            "Twilio number migration returned error payload. PhoneNumberSid: {PhoneNumberSid}, Code: {Code}, Status: {Status}, Message: {Message}",
            request.PhoneNumberSid, response.Code, response.Status, response.Message);

        var errorMessage = string.IsNullOrWhiteSpace(response.Message)
            ? "Twilio number migration failed."
            : $"Twilio number migration failed: {response.Message}";

        throw new InvalidOperationException(errorMessage);
    }
}
