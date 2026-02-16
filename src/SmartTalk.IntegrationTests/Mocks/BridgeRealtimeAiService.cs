using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Autofac;
using Newtonsoft.Json;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Services;
using SmartTalk.Core.Services.Timer;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SmartTalk.IntegrationTests.Mocks;

/// <summary>
/// Bridge mock that implements <see cref="IRealtimeAiService"/> by reading from a mock OpenAI WebSocket
/// (injected as <see cref="WebSocket"/> via DI) and translating V1 protocol messages into V2 callback
/// invocations. This allows existing V1-style integration tests to validate V2 business logic without
/// modifying test assertions.
/// </summary>
public class BridgeRealtimeAiService : IRealtimeAiService
{
    private readonly ILifetimeScope _scope;
    private readonly IInactivityTimerManager _timerManager;

    public BridgeRealtimeAiService(ILifetimeScope scope, IInactivityTimerManager timerManager)
    {
        _scope = scope;
        _timerManager = timerManager;
    }

    public async Task ConnectAsync(RealtimeSessionOptions options, CancellationToken cancellationToken)
    {
        if (!_scope.TryResolve<WebSocket>(out var openaiWs))
            return;

        var twilioWs = options.WebSocket;
        var modelConfig = options.ModelConfig;

        var callSid = "";
        var streamSid = "";
        var shouldSendToOpenAi = true;
        var initialConversationSent = false;
        var transcriptions = new List<(AiSpeechAssistantSpeaker Speaker, string Text)>();

        // 1. Send session.update to the mock OpenAI WS (same as V1's SendSessionUpdateAsync)
        await SendSessionUpdateAsync(openaiWs, modelConfig, cancellationToken).ConfigureAwait(false);

        // 2. Read the twilio "start" event first to capture callSid before parallel processing
        (callSid, streamSid) = await ReadTwilioStartEventAsync(twilioWs, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(callSid) && options.OnClientStartAsync != null)
        {
            await options.OnClientStartAsync(Guid.NewGuid().ToString(), new Dictionary<string, string>
            {
                ["callSid"] = callSid,
                ["streamSid"] = streamSid
            }).ConfigureAwait(false);
        }

        // 3. Build V2 session actions that write V1-compatible protocol messages
        var capturedStreamSid = streamSid;
        var actions = new RealtimeAiSessionActions
        {
            SendTextToProviderAsync = async text =>
            {
                // V1 format: conversation.item.create (message) + response.create
                await SendToWsAsync(openaiWs, new
                {
                    type = "conversation.item.create",
                    item = new
                    {
                        type = "message",
                        role = "user",
                        content = new[] { new { type = "input_text", text } }
                    }
                }, cancellationToken).ConfigureAwait(false);

                await SendToWsAsync(openaiWs, new { type = "response.create" }, cancellationToken).ConfigureAwait(false);
            },
            SendAudioToClientAsync = async base64Audio =>
            {
                await SendToWsAsync(twilioWs, new
                {
                    @event = "media",
                    streamSid = capturedStreamSid,
                    media = new { payload = base64Audio }
                }, cancellationToken).ConfigureAwait(false);
            },
            SuspendClientAudioToProvider = () => shouldSendToOpenAi = false,
            ResumeClientAudioToProvider = () => shouldSendToOpenAi = true,
            GetRecordedAudioSnapshotAsync = () => Task.FromResult(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 })
        };

        // 4. Run dual WS loops in parallel (same as V1's Task.WhenAll(receiveTask, sendTask))
        var twilioTask = ProcessTwilioLoopAsync(
            twilioWs, openaiWs, options, () => shouldSendToOpenAi, transcriptions, cancellationToken);

        var openaiTask = ProcessOpenAiLoopAsync(
            openaiWs, twilioWs, options, actions,
            () => callSid, () => capturedStreamSid,
            initialConversationSent, transcriptions, cancellationToken);

        await Task.WhenAll(twilioTask, openaiTask).ConfigureAwait(false);
    }

    private async Task ProcessTwilioLoopAsync(
        WebSocket twilioWs, WebSocket openaiWs,
        RealtimeSessionOptions options,
        Func<bool> shouldSendToOpenAi,
        List<(AiSpeechAssistantSpeaker, string)> transcriptions,
        CancellationToken ct)
    {
        var buffer = new byte[1024 * 10];
        try
        {
            while (twilioWs.State == WebSocketState.Open)
            {
                var result = await twilioWs.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await openaiWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "Twilio closed", ct).ConfigureAwait(false);
                    break;
                }

                if (result.Count > 0)
                {
                    using var doc = JsonSerializer.Deserialize<JsonDocument>(buffer.AsSpan(0, result.Count));
                    var eventMsg = doc?.RootElement.GetProperty("event").GetString();

                    switch (eventMsg)
                    {
                        case "media":
                            if (shouldSendToOpenAi())
                            {
                                var payload = doc.RootElement.GetProperty("media").GetProperty("payload").GetString();
                                if (!string.IsNullOrEmpty(payload))
                                    await SendToWsAsync(openaiWs, new { type = "input_audio_buffer.append", audio = payload }, ct).ConfigureAwait(false);
                            }
                            break;

                        case "stop":
                            if (options.OnTranscriptionsCompletedAsync != null)
                                await options.OnTranscriptionsCompletedAsync(Guid.NewGuid().ToString(), transcriptions.AsReadOnly()).ConfigureAwait(false);

                            if (options.OnClientStopAsync != null)
                                await options.OnClientStopAsync(Guid.NewGuid().ToString()).ConfigureAwait(false);
                            break;
                    }
                }
            }
        }
        catch (WebSocketException) { }
    }

    private async Task ProcessOpenAiLoopAsync(
        WebSocket openaiWs, WebSocket twilioWs,
        RealtimeSessionOptions options, RealtimeAiSessionActions actions,
        Func<string> getCallSid, Func<string> getStreamSid,
        bool initialConversationSent, List<(AiSpeechAssistantSpeaker, string)> transcriptions,
        CancellationToken ct)
    {
        var greetingSent = initialConversationSent;
        var buffer = new byte[1024 * 30];
        try
        {
            while (openaiWs.State == WebSocketState.Open)
            {
                var result = await openaiWs.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close || result.Count == 0)
                    break;

                var value = Encoding.UTF8.GetString(buffer, 0, result.Count);
                JsonDocument doc;
                try
                {
                    doc = JsonSerializer.Deserialize<JsonDocument>(value);
                }
                catch
                {
                    continue;
                }

                var type = doc?.RootElement.GetProperty("type").GetString();

                switch (type)
                {
                    case "session.updated":
                        if (!greetingSent && options.OnSessionReadyAsync != null)
                        {
                            await options.OnSessionReadyAsync(actions).ConfigureAwait(false);
                            greetingSent = true;
                        }
                        break;

                    case "response.audio.delta":
                        if (doc.RootElement.TryGetProperty("delta", out var delta))
                        {
                            await SendToWsAsync(twilioWs, new
                            {
                                @event = "media",
                                streamSid = getStreamSid(),
                                media = new { payload = delta.GetString() }
                            }, ct).ConfigureAwait(false);
                        }
                        break;

                    case "input_audio_buffer.speech_started":
                        var sid = getCallSid();
                        if (!string.IsNullOrEmpty(sid))
                            _timerManager.StopTimer(sid);

                        await SendToWsAsync(twilioWs, new
                        {
                            @event = "clear",
                            streamSid = getStreamSid()
                        }, ct).ConfigureAwait(false);
                        break;

                    case "conversation.item.input_audio_transcription.completed":
                        if (doc.RootElement.TryGetProperty("transcript", out var userTranscript))
                            transcriptions.Add((AiSpeechAssistantSpeaker.User, userTranscript.GetString()));
                        break;

                    case "response.audio_transcript.done":
                        if (doc.RootElement.TryGetProperty("transcript", out var aiTranscript))
                            transcriptions.Add((AiSpeechAssistantSpeaker.Ai, aiTranscript.GetString()));
                        break;

                    case "response.done":
                        await HandleResponseDoneAsync(doc, options, actions, getCallSid, ct).ConfigureAwait(false);
                        break;
                }
            }
        }
        catch (WebSocketException) { }
    }

    private async Task HandleResponseDoneAsync(
        JsonDocument doc, RealtimeSessionOptions options,
        RealtimeAiSessionActions actions, Func<string> getCallSid,
        CancellationToken ct)
    {
        var response = doc.RootElement.GetProperty("response");

        if (response.TryGetProperty("output", out var output) && output.GetArrayLength() > 0)
        {
            foreach (var element in output.EnumerateArray())
            {
                if (element.GetProperty("type").GetString() != "function_call") continue;

                var functionCallData = new RealtimeAiWssFunctionCallData
                {
                    CallId = element.GetProperty("call_id").GetString(),
                    FunctionName = element.GetProperty("name").GetString(),
                    ArgumentsJson = element.GetProperty("arguments").GetString()
                };

                if (options.OnFunctionCallAsync != null)
                {
                    var fcResult = await options.OnFunctionCallAsync(functionCallData, actions).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(fcResult?.Output))
                    {
                        // V1 format: conversation.item.create (function_call_output) + response.create
                        // Use the same openaiWs that the actions reference
                        // We need to write to the openaiWs directly through the actions' internal WS reference
                        // Actually, we use the SendToWsAsync which writes to the mock's SentMessages
                        if (_scope.TryResolve<WebSocket>(out var openaiWs))
                        {
                            await SendToWsAsync(openaiWs, new
                            {
                                type = "conversation.item.create",
                                item = new
                                {
                                    type = "function_call_output",
                                    call_id = functionCallData.CallId,
                                    output = fcResult.Output
                                }
                            }, ct).ConfigureAwait(false);

                            await SendToWsAsync(openaiWs, new { type = "response.create" }, ct).ConfigureAwait(false);
                        }
                    }
                }

                break; // V1 breaks after first function_call in the output array
            }
        }

        // V1 starts inactivity timer after every response.done
        var callSid = getCallSid();
        if (!string.IsNullOrEmpty(callSid))
        {
            _timerManager.StartTimer(callSid, TimeSpan.FromSeconds(60), () => Task.CompletedTask);
        }
    }

    private static async Task<(string CallSid, string StreamSid)> ReadTwilioStartEventAsync(
        WebSocket twilioWs, CancellationToken ct)
    {
        var buffer = new byte[1024 * 10];

        while (twilioWs.State == WebSocketState.Open)
        {
            var result = await twilioWs.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close || result.Count == 0)
                break;

            using var doc = JsonSerializer.Deserialize<JsonDocument>(buffer.AsSpan(0, result.Count));
            var eventMsg = doc?.RootElement.GetProperty("event").GetString();

            if (eventMsg == "start")
            {
                var callSid = doc.RootElement.GetProperty("start").GetProperty("callSid").GetString();
                var streamSid = doc.RootElement.GetProperty("start").GetProperty("streamSid").GetString();
                return (callSid, streamSid);
            }
        }

        return (null, null);
    }

    private static async Task SendSessionUpdateAsync(WebSocket ws, RealtimeAiModelConfig modelConfig, CancellationToken ct)
    {
        var sessionUpdate = new
        {
            type = "session.update",
            session = new
            {
                turn_detection = modelConfig.TurnDetection ?? (object)new { type = "server_vad" },
                input_audio_format = "g711_ulaw",
                output_audio_format = "g711_ulaw",
                voice = string.IsNullOrEmpty(modelConfig.Voice) ? "alloy" : modelConfig.Voice,
                instructions = modelConfig.Prompt,
                modalities = new[] { "text", "audio" },
                temperature = 0.8,
                input_audio_transcription = new { model = "whisper-1" },
                input_audio_noise_reduction = modelConfig.InputAudioNoiseReduction,
                tools = modelConfig.Tools?.Count > 0 ? (object)modelConfig.Tools : null
            }
        };

        await SendToWsAsync(ws, sessionUpdate, ct).ConfigureAwait(false);
    }

    private static async Task SendToWsAsync(WebSocket ws, object message, CancellationToken ct)
    {
        var json = JsonConvert.SerializeObject(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
    }
}
