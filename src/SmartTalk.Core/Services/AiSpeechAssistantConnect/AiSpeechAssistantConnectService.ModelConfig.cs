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
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private async Task<RealtimeAiModelConfig> BuildModelConfigAsync(
        SessionBusinessContext ctx, string resolvedPrompt, CancellationToken cancellationToken)
    {
        var assistant = ctx.Assistant;

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
            ModelName = ctx.ModelName,
            ModelLanguage = assistant.ModelLanguage,
            Prompt = resolvedPrompt,
            Tools = configs
                .Where(x => x.Type == AiSpeechAssistantSessionConfigType.Tool)
                .Select(x => x.Config)
                .ToList(),
            TurnDetection = configs.FirstOrDefault(x => x.Type == AiSpeechAssistantSessionConfigType.TurnDirection).Config,
            InputAudioNoiseReduction = configs.FirstOrDefault(x => x.Type == AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction).Config
        };
    }

    private RealtimeSessionOptions BuildSessionOptions(
        ConnectAiSpeechAssistantCommand command,
        SessionBusinessContext ctx,
        RealtimeAiModelConfig modelConfig,
        AiSpeechAssistantTimer timer)
    {
        var orderRecordType = command.OrderRecordType;

        return new RealtimeSessionOptions
        {
            ClientConfig = new RealtimeAiClientConfig
            {
                Client = RealtimeAiClient.Twilio
            },
            ModelConfig = modelConfig,
            ConnectionProfile = new RealtimeAiConnectionProfile
            {
                ProfileId = ctx.Assistant.Id.ToString()
            },
            WebSocket = command.TwilioWebSocket,
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
            OnSessionReadyAsync = async actions =>
            {
                await actions.SendTextToProviderAsync($"Greet the user with: '{ctx.Knowledge?.Greetings}'").ConfigureAwait(false);
            },
            OnClientStartAsync = async (sessionId, metadata) =>
            {
                metadata.TryGetValue("callSid", out var callSid);
                metadata.TryGetValue("streamSid", out var streamSid);

                ctx.CallSid = callSid;
                ctx.StreamSid = streamSid;

                _backgroundJobClient.Enqueue<IMediator>(x => x.SendAsync(new RecordAiSpeechAssistantCallCommand
                {
                    CallSid = ctx.CallSid, Host = ctx.Host
                }, CancellationToken.None), HangfireConstants.InternalHostingRecordPhoneCall);

                if (!ctx.IsInAiServiceHours && ctx.IsEnableManualService)
                {
                    _backgroundJobClient.Enqueue<IMediator>(x => x.SendAsync(new TransferHumanServiceCommand
                    {
                        CallSid = ctx.CallSid,
                        HumanPhone = ctx.TransferCallNumber
                    }, CancellationToken.None));
                }
            },
            OnFunctionCallAsync = async (functionCallData, actions) =>
                await OnFunctionCallAsync(ctx, functionCallData, actions, CancellationToken.None).ConfigureAwait(false),
            OnTranscriptionsCompletedAsync = async (sessionId, transcriptions) =>
            {
                var streamContext = new AiSpeechAssistantStreamContextDto
                {
                    CallSid = ctx.CallSid,
                    StreamSid = ctx.StreamSid,
                    Host = ctx.Host,
                    Assistant = ctx.Assistant,
                    Knowledge = ctx.Knowledge,
                    OrderItems = ctx.OrderItems,
                    UserInfo = ctx.UserInfo,
                    LastUserInfo = ctx.LastUserInfo,
                    IsTransfer = ctx.IsTransfer,
                    HumanContactPhone = ctx.HumanContactPhone,
                    ConversationTranscription = transcriptions.Select(t => (t.Speaker, t.Text)).ToList()
                };

                _backgroundJobClient.Enqueue<IAiSpeechAssistantProcessJobService>(x =>
                    x.RecordAiSpeechAssistantCallAsync(streamContext, orderRecordType, CancellationToken.None));
            },
            OnRecordingCompleteAsync = async (sessionId, wavBytes) =>
            {
                Log.Information("[AiSpeechAssistantConnect] Recording complete, SessionId: {SessionId}, Size: {Size}",
                    sessionId, wavBytes.Length);
            }
        };
    }
}
