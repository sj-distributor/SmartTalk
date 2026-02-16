using Mediator.Net;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private async Task<RealtimeAiModelConfig> BuildModelConfigAsync(CancellationToken cancellationToken)
    {
        var assistant = _ctx.Assistant;

        var functionCalls = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantFunctionCallByAssistantIdsAsync(
                [assistant.Id], assistant.ModelProvider, true, cancellationToken).ConfigureAwait(false);

        var configs = functionCalls
            .Where(x => !string.IsNullOrWhiteSpace(x.Content))
            .Select(x => (x.Type, Config: JsonConvert.DeserializeObject<object>(x.Content)))
            .ToList();

        return new RealtimeAiModelConfig
        {
            Provider = assistant.ModelProvider,
            ServiceUrl = assistant.ModelUrl ?? AiSpeechAssistantStore.DefaultUrl,
            Voice = assistant.ModelVoice ?? "alloy",
            ModelName = assistant.ModelName,
            ModelLanguage = assistant.ModelLanguage,
            Prompt = _ctx.Prompt,
            Tools = configs
                .Where(x => x.Type == AiSpeechAssistantSessionConfigType.Tool)
                .Select(x => x.Config)
                .ToList(),
            TurnDetection = configs.FirstOrDefault(x => x.Type == AiSpeechAssistantSessionConfigType.TurnDirection).Config,
            InputAudioNoiseReduction = configs.FirstOrDefault(x => x.Type == AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction).Config
        };
    }

    private RealtimeSessionOptions BuildSessionOptions(
        RealtimeAiModelConfig modelConfig,
        AiSpeechAssistantTimer timer)
    {
        return new RealtimeSessionOptions
        {
            ClientConfig = new RealtimeAiClientConfig
            {
                Client = RealtimeAiClient.Twilio
            },
            ModelConfig = modelConfig,
            ConnectionProfile = new RealtimeAiConnectionProfile
            {
                ProfileId = _ctx.Assistant.Id.ToString()
            },
            WebSocket = _ctx.TwilioWebSocket,
            Region = RealtimeAiServerRegion.US,
            EnableRecording = true,
            IdleFollowUp = timer != null
                ? new RealtimeSessionIdleFollowUp
                {
                    TimeoutSeconds = timer.TimeSpanSeconds,
                    FollowUpMessage = timer.AlterContent,
                    SkipRounds = timer.SkipRound
                }
                : null,
            OnSessionReadyAsync = HandleSessionReadyAsync,
            OnClientStartAsync = HandleClientStartAsync,
            OnFunctionCallAsync = (data, actions) =>
                OnFunctionCallAsync(data, actions, CancellationToken.None),
            OnTranscriptionsCompletedAsync = HandleTranscriptionsCompletedAsync,
            OnRecordingCompleteAsync = HandleRecordingCompleteAsync
        };
    }

    private async Task HandleSessionReadyAsync(RealtimeAiSessionActions actions)
    {
        await actions.SendTextToProviderAsync($"Greet the user with: '{_ctx.Knowledge?.Greetings}'").ConfigureAwait(false);
    }

    private Task HandleClientStartAsync(string sessionId, Dictionary<string, string> metadata)
    {
        metadata.TryGetValue("callSid", out var callSid);
        metadata.TryGetValue("streamSid", out var streamSid);

        _ctx.CallSid = callSid;
        _ctx.StreamSid = streamSid;

        _backgroundJobClient.Enqueue<IMediator>(x => x.SendAsync(new RecordAiSpeechAssistantCallCommand
        {
            CallSid = _ctx.CallSid, Host = _ctx.Host
        }, CancellationToken.None), HangfireConstants.InternalHostingRecordPhoneCall);

        if (!_ctx.IsInAiServiceHours && _ctx.IsEnableManualService)
        {
            _backgroundJobClient.Enqueue<IMediator>(x => x.SendAsync(new TransferHumanServiceCommand
            {
                CallSid = _ctx.CallSid,
                HumanPhone = _ctx.TransferCallNumber
            }, CancellationToken.None));
        }

        return Task.CompletedTask;
    }

    private Task HandleTranscriptionsCompletedAsync(
        string sessionId, IReadOnlyList<(AiSpeechAssistantSpeaker Speaker, string Text)> transcriptions)
    {
        var streamContext = new AiSpeechAssistantStreamContextDto
        {
            CallSid = _ctx.CallSid,
            StreamSid = _ctx.StreamSid,
            Host = _ctx.Host,
            Assistant = _ctx.Assistant,
            Knowledge = _ctx.Knowledge,
            OrderItems = _ctx.OrderItems,
            UserInfo = _ctx.UserInfo,
            LastUserInfo = _ctx.LastUserInfo,
            IsTransfer = _ctx.IsTransfer,
            HumanContactPhone = _ctx.HumanContactPhone,
            ConversationTranscription = transcriptions.Select(t => (t.Speaker, t.Text)).ToList()
        };

        _backgroundJobClient.Enqueue<IAiSpeechAssistantProcessJobService>(x =>
            x.RecordAiSpeechAssistantCallAsync(streamContext, _ctx.OrderRecordType, CancellationToken.None));

        return Task.CompletedTask;
    }

    private Task HandleRecordingCompleteAsync(string sessionId, byte[] wavBytes)
    {
        Log.Information("[AiAssistant] Recording complete, SessionId: {SessionId}, Size: {Size}bytes",
            sessionId, wavBytes.Length);

        return Task.CompletedTask;
    }
}
