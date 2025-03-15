using Twilio;
using Serilog;
using System.Text;
using Twilio.TwiML;
using Mediator.Net;
using Newtonsoft.Json;
using System.Text.Json;
using Twilio.TwiML.Voice;
using SmartTalk.Core.Ioc;
using Twilio.AspNet.Core;
using System.Net.WebSockets;
using AutoMapper;
using SmartTalk.Core.Constants;
using Microsoft.AspNetCore.Http;
using OpenAI.Chat;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Messages.Constants;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Dto.OpenAi;
using Twilio.Rest.Api.V2010.Account;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Core.Settings.Twilio;
using SmartTalk.Core.Settings.ZhiPuAi;
using Task = System.Threading.Tasks.Task;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Events.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.PhoneOrder;
using Twilio.Types;
using JsonSerializer = System.Text.Json.JsonSerializer;
using RecordingResource = Twilio.Rest.Api.V2010.Account.Call.RecordingResource;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public interface IAiSpeechAssistantService : IScopedDependency
{
    CallAiSpeechAssistantResponse CallAiSpeechAssistant(CallAiSpeechAssistantCommand command);

    Task<OutboundCallFromAiSpeechAssistantResponse> OutboundCallFromAiSpeechAssistantAsync(OutboundCallFromAiSpeechAssistantCommand command, CancellationToken cancellationToken);

    Task<AiSpeechAssistantConnectCloseEvent> ConnectAiSpeechAssistantAsync(ConnectAiSpeechAssistantCommand command, CancellationToken cancellationToken);

    Task RecordAiSpeechAssistantCallAsync(RecordAiSpeechAssistantCallCommand command, CancellationToken cancellationToken);

    Task ReceivePhoneRecordingStatusCallbackAsync(ReceivePhoneRecordingStatusCallbackCommand command, CancellationToken cancellationToken);
    
    Task TransferHumanServiceAsync(TransferHumanServiceCommand command, CancellationToken cancellationToken);

    Task HangupCallAsync(string callSid, CancellationToken cancellationToken);
}

public class AiSpeechAssistantService : IAiSpeechAssistantService
{
    private readonly IMapper _mapper;
    private readonly OpenAiSettings _openAiSettings;
    private readonly TwilioSettings _twilioSettings;
    private readonly ISmartiesClient _smartiesClient;
    private readonly ZhiPuAiSettings _zhiPuAiSettings;
    private readonly IPhoneOrderService _phoneOrderService;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    public AiSpeechAssistantService(
        IMapper mapper,
        OpenAiSettings openAiSettings,
        TwilioSettings twilioSettings,
        ISmartiesClient smartiesClient,
        ZhiPuAiSettings zhiPuAiSettings,
        IPhoneOrderService phoneOrderService,
        ISmartTalkHttpClientFactory httpClientFactory,
        IPhoneOrderDataProvider phoneOrderDataProvider,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _mapper = mapper;
        _openAiSettings = openAiSettings;
        _twilioSettings = twilioSettings;
        _smartiesClient = smartiesClient;
        _zhiPuAiSettings = zhiPuAiSettings;
        _phoneOrderService = phoneOrderService;
        _httpClientFactory = httpClientFactory;
        _backgroundJobClient = backgroundJobClient;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
    }

    public CallAiSpeechAssistantResponse CallAiSpeechAssistant(CallAiSpeechAssistantCommand command)
    {
        var response = new VoiceResponse();
        var connect = new Connect();

        connect.Stream(url: $"wss://{command.Host}/api/AiSpeechAssistant/connect/{command.From}/{command.To}");
        
        response.Append(connect);

        var twiMlResult = Results.Extensions.TwiML(response);
        Log.Information("TwiMl result: {@TwiMlResult}", twiMlResult);

        return new CallAiSpeechAssistantResponse { Data = twiMlResult };
    }

    public async Task<OutboundCallFromAiSpeechAssistantResponse> OutboundCallFromAiSpeechAssistantAsync(OutboundCallFromAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        var connect = new Connect();
        var response = new VoiceResponse();

        connect.Stream(url: $"wss://{command.Host}/api/AiSpeechAssistant/connect/{command.From}/{command.To}");
        
        response.Append(connect);
        
        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);
        
        await CallResource.CreateAsync(to: new PhoneNumber(command.To), from: new PhoneNumber(command.From), twiml: new Twiml(response.ToString())).ConfigureAwait(false);

        return new OutboundCallFromAiSpeechAssistantResponse();
    }

    public async Task<AiSpeechAssistantConnectCloseEvent> ConnectAiSpeechAssistantAsync(ConnectAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        Log.Information($"The call from {command.From} to {command.To} is connected");

        var (assistant, knowledgeBase) = await BuildingAiSpeechAssistantKnowledgeBaseAsync(command.From, command.To, command.AssistantId, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(knowledgeBase)) return new AiSpeechAssistantConnectCloseEvent();

        var humanContact = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantHumanContactByAssistantIdAsync(assistant.Id, cancellationToken).ConfigureAwait(false);

        //connect openai socket
        var openaiWebSocket = await ConnectOpenAiRealTimeSocketAsync(assistant, knowledgeBase, cancellationToken).ConfigureAwait(false);
        
        var context = new AiSpeechAssistantStreamContextDto
        {
            Host = command.Host,
            LastPrompt = knowledgeBase,
            HumanContactPhone = humanContact?.HumanPhone,
            LastUserInfo = new AiSpeechAssistantUserInfoDto
            {
                PhoneNumber = command.From
            },
            Assistant = _mapper.Map<AiSpeechAssistantDto>(assistant)
        };
        
        var receiveFromTwilioTask = ReceiveFromTwilioAsync(command.TwilioWebSocket, openaiWebSocket, context);
        var sendToTwilioTask = SendToTwilioAsync(command.TwilioWebSocket, openaiWebSocket, context, cancellationToken);

        try
        {
            await Task.WhenAll(receiveFromTwilioTask, sendToTwilioTask);
        }
        catch (Exception ex)
        {
            Log.Information("Error in one of the tasks: " + ex.Message);
        }
        
        return new AiSpeechAssistantConnectCloseEvent();
    }

    public async Task RecordAiSpeechAssistantCallAsync(RecordAiSpeechAssistantCallCommand command, CancellationToken cancellationToken)
    {
        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);
        
        await RecordingResource.CreateAsync(pathCallSid: command.CallSid, recordingStatusCallbackMethod: Twilio.Http.HttpMethod.Post,
            recordingStatusCallback: new Uri($"https://{command.Host}/api/AiSpeechAssistant/recording/callback"));
    }

    public async Task ReceivePhoneRecordingStatusCallbackAsync(ReceivePhoneRecordingStatusCallbackCommand command, CancellationToken cancellationToken)
    {
        Log.Information("Handling receive phone record: {@command}", command);

        var (record, agent, aiSpeechAssistant) = await _phoneOrderDataProvider.GetRecordWithAgentAndAssistantAsync(command.CallSid, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get phone order record: {@record}", record);

        record.Url = command.RecordingUrl;
        record.Status = PhoneOrderRecordStatus.Sent;
        
        ChatClient client = new("gpt-4o-audio-preview", _openAiSettings.ApiKey);
        var audioFileRawBytes = await _httpClientFactory.GetAsync<byte[]>(record.Url, cancellationToken).ConfigureAwait(false);
        var audioData = BinaryData.FromBytes(audioFileRawBytes);
        List<ChatMessage> messages =
        [
            new SystemChatMessage(string.IsNullOrEmpty(aiSpeechAssistant?.CustomRecordAnalyzePrompt)
                ? "你是一名電話錄音的分析員，通過聽取錄音內容和語氣情緒作出精確分析，冩出一份分析報告。\n\n分析報告的格式：交談主題：xxx\n\n 內容摘要:xxx \n\n 客人情感與情緒: xxx \n\n 待辦事件: \n1.xxx\n2.xxx \n\n 客人下單內容(如果沒有則忽略)：1. 牛肉(1箱)\n2.雞腿肉(1箱)" 
                : aiSpeechAssistant.CustomRecordAnalyzePrompt),
            new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(audioData, ChatInputAudioFormat.Wav)),
            new UserChatMessage("幫我根據錄音生成分析報告：")
        ];
        
        ChatCompletionOptions options = new() { ResponseModalities = ChatResponseModalities.Text };

        ChatCompletion completion = await client.CompleteChatAsync(messages, options, cancellationToken);
        Log.Information("sales record analyze report:" + completion.Content.FirstOrDefault()?.Text);
        record.TranscriptionText = completion.Content.FirstOrDefault()?.Text;

        if (agent.SourceSystem == AgentSourceSystem.Smarties)
            await _smartiesClient.CallBackSmartiesAiSpeechAssistantRecordAsync(new AiSpeechAssistantCallBackRequestDto { CallSid = command.CallSid, RecordUrl = record.Url, RecordAnalyzeReport =  record.TranscriptionText }, cancellationToken).ConfigureAwait(false);
        
        if (!string.IsNullOrEmpty(agent.WechatRobotKey))
            await _phoneOrderService.SendWorkWeChatRobotNotifyAsync(audioFileRawBytes, agent.WechatRobotKey, "錄音分析報告：\n" + record.TranscriptionText, cancellationToken).ConfigureAwait(false);

        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task TransferHumanServiceAsync(TransferHumanServiceCommand command, CancellationToken cancellationToken)
    {
        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);
        
        var call = await CallResource.UpdateAsync(
            pathSid: command.CallSid,
            twiml: $"<Response>\n    <Dial>\n      <Number>{command.HumanPhone}</Number>\n    </Dial>\n  </Response>"
        );
    }

    public async Task HangupCallAsync(string callSid, CancellationToken cancellationToken)
    {
        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);
        
        await CallResource.UpdateAsync(
            pathSid: callSid,
            status: CallResource.UpdateStatusEnum.Completed
        );
    }

    private async Task<(Domain.AISpeechAssistant.AiSpeechAssistant Assistant, string Prompt)> BuildingAiSpeechAssistantKnowledgeBaseAsync(string from, string to, int? assistantId, CancellationToken cancellationToken)
    {
        var (assistant, promptTemplate, userProfile) = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantInfoByNumbersAsync(from, to, assistantId, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Matching Ai speech assistant: {@Assistant}、{@PromptTemplate}、{@UserProfile}", assistant, promptTemplate, userProfile);

        if (assistant == null || promptTemplate == null || string.IsNullOrEmpty(promptTemplate.Template)) return (assistant, string.Empty);

        var pstTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        var currentTime = pstTime.ToString("yyyy-MM-dd HH:mm:ss");
        
        var finalPrompt = promptTemplate.Template
            .Replace("#{user_profile}", string.IsNullOrEmpty(userProfile?.ProfileJson) ? " " : userProfile.ProfileJson)
            .Replace("#{current_time}", currentTime)
            .Replace("#{customer_phone}", from.StartsWith("+1") ? from[2..] : from);
        
        Log.Information($"The final prompt: {finalPrompt}");

        return (assistant, finalPrompt);
    }

    private async Task<WebSocket> ConnectOpenAiRealTimeSocketAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistant, string prompt, CancellationToken cancellationToken)
    {
        var openAiWebSocket = new ClientWebSocket();
        openAiWebSocket.Options.SetRequestHeader("Authorization", GetAuthorizationHeader(assistant));
        openAiWebSocket.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");

        var url = string.IsNullOrEmpty(assistant.Url) ? AiSpeechAssistantStore.DefaultUrl : assistant.Url;

        await openAiWebSocket.ConnectAsync(new Uri(url), cancellationToken).ConfigureAwait(false);

        await SendSessionUpdateAsync(openAiWebSocket, assistant, prompt).ConfigureAwait(false);
        
        return openAiWebSocket;
    }

    private string GetAuthorizationHeader(Domain.AISpeechAssistant.AiSpeechAssistant assistant)
    {
        return assistant.Provider switch
        {
            AiSpeechAssistantProvider.OpenAi => $"Bearer {_openAiSettings.ApiKey}",
            AiSpeechAssistantProvider.ZhiPuAi => $"Bearer {_zhiPuAiSettings.ApiKey}",
            _ => throw new NotSupportedException(nameof(assistant.Provider))
        };
    }
    
    private async Task ReceiveFromTwilioAsync(WebSocket twilioWebSocket, WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context)
    {
        var buffer = new byte[1024 * 30];
        try
        {
            while (twilioWebSocket.State == WebSocketState.Open)
            {
                var result = await twilioWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                Log.Information("ReceiveFromTwilioAsync result: {result}", Encoding.UTF8.GetString(buffer, 0, result.Count));
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await openAiWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Twilio closed", CancellationToken.None);
                    break;
                }

                if (result.Count > 0)
                {
                    using var jsonDocument = JsonSerializer.Deserialize<JsonDocument>(buffer.AsSpan(0, result.Count));
                    var eventMessage = jsonDocument?.RootElement.GetProperty("event").GetString();
                    
                    switch (eventMessage)
                    {
                        case "connected":
                            break;
                        case "start":
                            context.StreamSid = jsonDocument.RootElement.GetProperty("start").GetProperty("streamSid").GetString();
                            context.CallSid = jsonDocument.RootElement.GetProperty("start").GetProperty("callSid").GetString();
                            context.ResponseStartTimestampTwilio = null;
                            context.LatestMediaTimestamp = 0;
                            context.LastAssistantItem = null;
                            
                            _backgroundJobClient.Enqueue<IMediator>(x=> x.SendAsync(new RecordAiSpeechAssistantCallCommand
                            {
                                CallSid = context.CallSid, Host = context.Host
                            }, CancellationToken.None));
                            break;
                        case "media":
                            var payload = jsonDocument?.RootElement.GetProperty("media").GetProperty("payload").GetString();
                            var audioAppend = new
                            {
                                type = "input_audio_buffer.append",
                                audio = payload
                            };
                            // await SendToWebSocketAsync(openAiWebSocket, audioAppend);
                            break;
                        case "stop":
                            _backgroundJobClient.Enqueue<IAiSpeechAssistantProcessJobService>(x => x.RecordAiSpeechAssistantCallAsync(context, CancellationToken.None));
                            break;
                    }
                }
            }
        }
        catch (WebSocketException ex)
        {
            Log.Information($"Receive from Twilio error: {ex.Message}");
        }
    }

    private async Task SendToTwilioAsync(WebSocket twilioWebSocket, WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context, CancellationToken cancellationToken)
    {
        Log.Information("Sending to twilio.");
        var buffer = new byte[1024 * 30];
        try
        {
            while (openAiWebSocket.State == WebSocketState.Open)
            {
                var result = await openAiWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                Log.Information("ReceiveFromOpenAi result: {result}", Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.Count > 0)
                {
                    var jsonDocument = JsonSerializer.Deserialize<JsonDocument>(buffer.AsSpan(0, result.Count));

                    Log.Information($"Received event: {jsonDocument?.RootElement.GetProperty("type").GetString()}");
                    
                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "error" && jsonDocument.RootElement.TryGetProperty("error", out var error))
                    {
                        Log.Information("Receive openai websocket error" + error.GetProperty("message").GetString());
                        
                        await SendToWebSocketAsync(openAiWebSocket, context.LastMessage);
                        await SendToWebSocketAsync(openAiWebSocket, new { type = "response.create" });
                    }

                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "session.updated")
                    {
                        Log.Information("Session updated successfully");
                    }

                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "response.audio.delta" && jsonDocument.RootElement.TryGetProperty("delta", out var delta))
                    {
                        var audioDelta = new
                        {
                            @event = "media",
                            streamSid = context.StreamSid,
                            media = new { payload = delta.GetString() }
                        };

                        await SendToWebSocketAsync(twilioWebSocket, audioDelta);
                        
                        if (context.ResponseStartTimestampTwilio == null)
                        {
                            context.ResponseStartTimestampTwilio = context.LatestMediaTimestamp;
                            if (context.ShowTimingMath)
                            {
                                Log.Information($"Setting start timestamp for new response: {context.ResponseStartTimestampTwilio}ms");
                            }
                        }

                        if (jsonDocument.RootElement.TryGetProperty("item_id", out var itemId))
                        {
                            context.LastAssistantItem = itemId.ToString();
                        }

                        await SendMark(twilioWebSocket, context);
                    }
                    
                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "input_audio_buffer.speech_started")
                    {
                        Log.Information("Speech started detected.");
                        if (!string.IsNullOrEmpty(context.LastAssistantItem))
                        {
                            Log.Information($"Interrupting response with id: {context.LastAssistantItem}");
                            await HandleSpeechStartedEventAsync(twilioWebSocket, openAiWebSocket, context);
                        }
                    }

                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "conversation.item.input_audio_transcription.completed")
                    {
                        var response = jsonDocument.RootElement.GetProperty("transcript").ToString();
                        context.ConversationTranscription.Add(new ValueTuple<AiSpeechAssistantSpeaker, string>(AiSpeechAssistantSpeaker.User, response));
                    }
                    
                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "response.audio_transcript.done")
                    {
                        var response = jsonDocument.RootElement.GetProperty("transcript").ToString();
                        context.ConversationTranscription.Add(new ValueTuple<AiSpeechAssistantSpeaker, string>(AiSpeechAssistantSpeaker.Ai, response));
                    }
                    
                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "response.done")
                    {
                        Log.Information("Response done, then send audio payload.");
                        
                        Log.Information("Sending first payload");
                        var first = new
                        {
                            type = "input_audio_buffer.append",
                            audio = "UklGRgBXAABXQVZFZm10IBIAAAAHAAEAQB8AAEAfAAABAAgAAABmYWN0BAAAAKtWAABMSVNUGgAAAElORk9JU0ZUDQAAAExhdmY2MS4xLjEwMAAAZGF0YatWAAD//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////37///9+/////////////////37//35+////////fn7//37///9+fv//fv///////////////////35+fn5+fn5+fv/////////////+/v79/f7+/35+fv//fn19fX7///9+fv7+/v79/f7+/v39/f99e3p5eXp6enp6enx9fv9+//7+/v79/P3+///+/v5+fXx7enp7fHx8e3x8fv78+/z7+/v6+fn5+fn5+Pb09fX19PT09vj5+/v8/P39/fz8/n59fn3//f5+fXx5eHd0c3JycnJ0dXZ3eXl4eHh4eXh4d3Z3eXl5e3t6eXp7fXx8ff77+fr7/Pz8/P3+fX18e3t9//37+fn8/P3+/v7+/v99ff/+/Pz+fHx8fH59e3l5en78+fb08vLz9fX3+vr8/n59fX7++fj7+vXz8vP09fb29/n7/37+/v3+fnp2dHRzc3V3eHV0dnl9/v97eXl8ff79/Pv8/Pz7+vt+fHp7/fr49/j4+Pf48/L1+Pr8/Pn7/X16e/79/n59/n55eXx8eXd1c3N3dnRzcW9sa2prbG1ubm5wcXJ2eXt+/v7+/fz69vb49/Lv7evs7e/w7+/v7u7w9ff08O/u8PHw8O/t7fD19vf4+Pj+e3h3eXh3c3Jxb25wcnBwc3Z8+/Tw7/D5fXp4dnd0cHFwcXZ8/Pj4+vz/fv5+fHl4c3BvcHJxb25vcHBwc3J0eHp7enz8+/5+/v1+fX18fv99e3p4eHZ2ev34+vn19vHu7uzr6+rr7/T18/Dz+Pn5+fT09PX3+fr7+fj6+v17eHd7/P59enZyb29vcG1samlsbG1wdHV6fXd2e/7/fH758/Py8O7w8/T28/Hw8O7u7u3t7vLz8/j7+/f5fXt+/n14cnBwcHBwdXZ2enp0bWtrbWtra2tucnV4fHp4c3V0eH7+/nz//fv18O/w9P18dXJzb21sbGxvdXv88/Lw7ezr7vT3+/7++/f39/j37u3u7+7y8/Dy9/j4+Pn4+/z9/P39/fv8fn7//nl0b25zdXV5eXd2eHl+/fr7eXJzfXt7/ntxcXh7+e7r7O/x7+rp6+vv+Pv2+Pv6/nh1cnJycnBtbG1vcnd7fn58dnN6fHlxbm93ent7/fr8+fPv7Orq7e/v8/Tt6evs8fb39fPy+H59ffv07e7v8fTy8O7w9fT4//3+fHt2bmlpZWRkZWRlZWVpam1ta2lqbG9ubm1saWpucHd8fXt6fvz/+vl+d3V2dXR3fHx4eH3/9O/x8PDz9PLz9Pb7e3l+/vj19/jz7uzp6Ojp6ers6+ns7fDx9/76+P399vT2+/v29PDy9PLy8/Z+dnVzdHNxcXJvb25tcXd7/n18eXFxdnZwbmplZWhpaGhnY2FhYWVnaWxye/317ero5+Tl6Ojp6u3v9Pn8/v96c29vbnN7/vT0+vz18PD08/b19fb08O7u6+3y8e/v7ezq6+3v8vl9d3BvbWppaGpra2hoaGltcHJ0c3N2e/jz9fT2+fr28/n7/P38+/v9fndzdHV1env89/n39vb18/P09vjx7/Dx9fj5+/15dnZvbnFzdnx+/f59ffz7+/r+/f78+Pt9fXt1b2xramxsbGtsbnR9fXt7eX3+/Pj1+P58/f37+fn7/P19eXZ4//r49/r59e/u7+3t7uzt7ezq6uzt6+rt7vP19PX9eHFwbmxra2xtbm5tb29ycnN4d29tbG92dXV0cnR7/vv3+Pl+evz29fj6+3t2evz6fnx4cnJ7/nt9fvz38u3s6+vt7+/u7u3w9fz78vXz9/38/Pz39/n5+Pn5+PLu9HhubW9wbWtubm5tbHJ2fPv+fH5+/X7/+fb19Pb59fb4+/x9enl9fnh3dnl+/Hx2cW1qamxtbW9saGNiaGxvbmxtc//38/Hv8O7t7Ovu8Pf8/v7+fnp4dXN6/Pfw7O3t7evn5+fp6+zs7e7v8vT9+/T5fHhydHNwbWpsbW1ucnv/+PDv6+ns7O3v8e7s7+70+v16eHV0c3BtbnBwcXBtbW9tb3h4c3Fvb3h8eXZ0dnV2fvv18fl7d3t6eXd0b3B3ffnz8vl+d3d1dnd+/f317+3u8/n9fX58ff38+vx4cnFzcXJ1c3N1e/z07u3u6ujo5+bm6e3y9/b6e3p8/v7++vX0+Pj5/P5+/P97enp2dHRvbnZ5d3d2eXh8+/x+fv/8ff3+fXx6fH19fHl2dnV4fXt2/vx7dnR1cXBubmtqbG1wcm9vcXJ5fn5+d3p6eP/18fHw7/Dt7O7w8/bx7erp6Ofq6+nn6Ovs7vt2bm1ubW1rbHByeX779PHu7Ozr6uvt9Pj5/H7+fXd1bm1vcHBtbGtsbnV8eXZwbGtqampqaWpuc3v59O7w+fz++ff49PX6+/f08/b4+//+//9+eXl4ef38/Px8enz+//fy7u30+P59fv/+fnt3d3z79vPt7PDy7+7u8/j8fn57/v1+e3Ftbm9vb29ubm9yevv28/L18+/u7u7t9Pn59/Hz9/v6/f98/PX5fn7+/v19eHBvb25wb2xsa2pqaWpsa21ubGxvevn19ff3+n16//x7dXB0cnBwdv5+fXp6/fj58+/v7u3u6+3s7/v9fXh3d3l6eHl6fPv28/bz8fDs7O3v7+7w9vb6fXZ5eXn/+vf19Pb28/X19fX19vXv7+7s7e/z9fTx7/Lz8fP4+/3+fXp7enp6eXl1cG5pZWJgX19gYWVlZ2ttcHd+fXp8fXp4d3RvbnV2d3d0dnp7eXZ1c3BwcHN6eHJ1e/r18Ozu8vTz8vLw9Pr7fn5++/Pw7+7t7ezr6uzu8PDv7+/u8/j+d3d2dXR4enl5//79+/z8+37//X56e379fXr9+ft+/vf39Pj8/H56eHv+9/Pw7/Dv8fLv7vX+e3v9fHt4c21sa21wbm5zeH79/fj49ft6cnBwbW9tb3JwcHn29Pj6+fb1+fx9e3ZvcHR3dnN1d3d5fHx7e3J4fvXx9Pr+/nz+/fr19Pj59/b59/j9eXr++/bx7+/x8vDu7Ovu9Pl+/359enJubmloa21ucHR8+fTu6ujq6uvt7/Dw8/l9fn19fn17c25sa21vb25udHf++Pv+/Pv29vTy/Xlyb3Jvbm5ramtwdnz28fD5+fPy8/l+fnVsZ2ZnZmZmaGpudf3y7Onn5OLh4+Xo6/Dw8fX6e3RubXJ0c3BubW9yefn3+Pb19ff6+fXw8PL2/Pn08ff4+X54d3z39Pb18fHy8/j3+3x2b25wcXNvbG1uc/338/T28/Pu6+nr7fH29fX6fntwbW1xcG5qaWhpbXJ0cHB2fv379vbz8vf5+PX2+Pf4/fz5+Pj7fn59ev37+vf28vXz8O/u7u7w9v94fXlvbW5ubW5vcHZ6//56c3BvcG9ubGloZ2dpbHBubGttb29vbm92ffXu7u7v7Ojk4ubp6+3v7+/u7u7w+Pv7+PP09Pb08vny7u7y+fr6+37/eG5ta2lrbnZ5//r49PDs7O7v8/n8fX53cG5raWdmaGtsbnV+9+/r6urq6enq6+77c21ramtscHFydXx9/fr6+fXx9Pb08fPz+fz+e3RvbGttb3Byd337+fXy7evt7u/x9/17eHJtZ2RkZmltd3t7eHj+/Hx5e3Z4enp8fv78+fT08vL8eXNvb25wcHBubnBzdXv8+n51d/739fX29vT09PHx8u3q6uXg4ubo6urp6Ono5ePh5uvx+np2cWxnZGJiZGVmZ2lsc3p7fHp8fHx9//38/Pv6/Pr09PT6/3p4/vn4+Pr39fDu8PT3+3l1dnZ0dHh1dv33+Pn7+v57e3V0bWpqbHJ4eXZxbm9xbGllZGVmaGpsb3Z6ffrz9/Tw7+zr7e7u7uzt7vD4+/x7eP3+//v07+zn5ebm6Ovr6+zt8fLw9vx9eXJzcnB1e/34+vv4/f5+e3p5eXh3c3BxdG5rZ2NjY2RmbHj28erm5ufq6/D3/XpycG9vcXd6fX7//Pr19Pb29fP2+Pf6+vf4+/t+dnFqaGhscnN3eXj57uvn5eTk5ejq7fL+e3Jvbmxpamlrbm1tbWtqbnNydXh0b3F0cHBvbWxub29vc3728fDt6+ro6erp6ern6Ors7vT7+vT5/v368O719vP29/f5/Ht9fvn09/19fn14d3d2dXNvb3B1fHdzcnN2/vf6/3h0eXpwa2hqbnJ2d3l+/f78/v5+/X53efz28+/v8fTz8O3t7/f9fnp2eXhwbW1vePz18vTz8/Hx9fn6/X16eHNubW1ta2xubW5xc25vdX347+3u7/Hw8/t5eHNubmxoam91eX17e/n27+vq6Ojr6+np6u3w+Xt5fvx8eHh7fX59/v359vHu6+rr6+3t8vTw8PZ+dXV0dndzcnV4fP78+Pr6fHl1cXF0e3V2dnNweXx6enx7eX377+7t8PX09PLz+/r8/Pb0+nZtbHJ1c3h7d3FwbnJ3eHduamlpaGZnaWhoaWxwcXJ0dnhzdXd8/n59+vHt6ujm5ePj5+zv7/H3/nx4dnN0eHt8fvz4+fj39vHv6+zt7vL4+Pt+eXJxbWtsbXN99fPw8PHz9fr5/nl2cG5xdHt7dG5tc3R3fHz7+vby7Ojp6+nm5+Xi4eLp7vL3/354c25pZWltcHRva2pqbXh+/Pr7+vXy7vD5/n7+fXh4enh5/f39/Xl1cm9sa2lqaGdoZWhoaWtscXh4e3t6/fbx9fHw+fr08PX29vz3+Pf28vDx8fDu7e3u7+3u7+/w8PH19vb3+n18eHZ7fn16e314e31+/3369PDu7vDu7Ozv+nx3d3Rwb25samhnZ2ZjYGJna21ubnd7dnF1dHFzd3dyc3p8+fHw7u728u3t8PTz8/Pz8PDt7evt6+vr6Obm5+Xh3+Hl5Ojq6evv/Xx4dnp+fn18fXtzbWhjY2BeXl5fY2JjaGpucHV6eX76+ft8ef7+e3t6eHf8+ff6+vT4+P3+/v76/Pv5+PPv7erq6urs7O7zfnd0bm1qaWhkZGRnaWpsbnv89u7v6+jl6Onq6uvr7/b59e7u7e3t7O7w9fz7/Hh2c3Jwbm9uamhqamxramppampvef308O3r7Ozs7O3x8PDw9Pp4b21sa2tsa2xtb3l89vfz8PT59/399vTv+P17fH799ezj4N7h6fR9dnF99/Pv8PN7a19XUExKSkpMTk9TWl9pfOvf3tza1tDPzszLysvMztHRz83O0Nniemtoa3zt497e4O5uWU1CPDk4OTw/P0BCRk1f49LKx8XDwsHBwcLFx8nLzcvOzMzQ19vSz8rN1HpUR0NFTmTh18/S1+ZmTj42My4tLC8zPEVLTkxPUmPmzcK8uLa3tre5vcLM0NDKxsPFx8nR0dXe8HV7/trb23RSRD8+Q05l5NXY325URT0zMS8uLjA2PUxXZV5gX3jcx723tLCztLe5vb7Fyc3NycXCwsfJzc7U4e5w+ffb3d5pTT87Oj1GWPLX1tnwXEs+NjEuLC0tMDU+SmX74uHe2czCu7WwsLCxs7a4ur3ByMvNy8rJzc3R2+ZmVkxOT2L+el5JPDUyMzhCVuXQzdHsW0U7MS0sLCwuMzpM+9HKycjKyMK7tK+trK2tr7K3ur/Gzc/S0c7MztDR3+X6X1ROUVNjXllJPjczMjQ6RFfm08/Xd04+Ni4sLCwtLzU+V9rKw8PExMC9ubSwr6+vr6+xtbm+w8rO0NLP0dPe3Ot9b1hPSUtLVlVRSD02MS8wNj1M+9DKyM/xTD4zLiwrLC0xOkzpyr+9vL29vLm2sq+ura2trrG2u7/IztPc3Nvd6PLubG5fUUtISEhNS0g+ODEuLjA4QlzXyMPG0mxIOi8sKyorLTI8VNfDu7q5u7u6t7Wyr66ura2usbe9xMzS1uDk5+Dk5+B+7mdYT0tJSU5KRz04Mi8vMzpFXdjLx8vXZEc5MCspKiotMTxL3MK8t7i4ube1sq+urKysq6yusrjAytbo92ZfW2RdZfx09GlVR0VBQ0hHQz03MzEyNT1IYtjMxsjQdks7MC0rKistMDlP6Me9urq5uLi1s7Kwr66ura6wtrzAyM/W5HJrZ2dj6+Xe3XRYS0pJTUtFPjYxLy8yOURR683Fv8DK/Eg3LiopKywvNT5Zybmyr7C0tra2srCwsLCwr66vtLvG1OtoX1hPTE1LVHfp3fZWREA/QkZBOjQvLi82PElYeNLFvr3B30g3LiwpLC0uLzVC7bqwr7G2urq0sa+vs7OwrauqrrfAztjZ61lHQEFGVd/V1ONgSktPTkxBNzAvMDM5PD5FVtvDu73KaEE2MS4sKCkrLTVL2b6yr7Gwr7Gzsra6ubayr6yusLW8v8LNek1APEBMS1Jy8eje7VdRT0dBPjgzMzY3O0JLY8+/urm8zGhHOjAtKCUoKy40RXfDs66vr6+xtLW5vr66t7OwsLS2t7i9yu1PRUNGQj9GU/3i42Rdb+xgSTw1MTM2NTU5Ql/Mvr3AxtD7SjguKCUnKCorM0fPuLGvrquoqayxuLu4tbe6ura1tLS3vsnT6lZMQzs+SFZQTk5a6tnyTEA+Pjs2MC8zPUxg6czAvb/Lc0Q6LyonJSQlLThM3b60raalpaipq62wtry9vb7DxcC9wcnQ2OP3WUhHS09FQUJNYGBRR0lKSD42MzQ4OjxCUuTNzNPe+VM/Mi0oKCopKi5C8cK3rqqloaSoqamssbi/w8PFztfLxMva3t3mdkxJSU9LQD9IVlhZTExUVUs+Ozs9PT1BTe3QzMvKzOFVPjUvKykpKSovO1DQvLCrp6SjpKanqq2yub7GzNjj3ed0ZvF+aFhNVVJVSEJETU5IRkNIS0c+Ozs7Ozo9RVbv2M7LyM7wT0A2Ly4qKCouNDpOzrqwqqelo6OlqKutsri+ytLX3ud4bmhrbWdcTVxgXU5KTVNWTUtISk1JQz4+Pj09P0lZ+d7V0M/eYkg8My0rKSgoLDE6TNa9s6unpaSjpKeqrbK5vsnc7/x4aGJl9+zn8Gju8PhZTU1UVExJRUdJRkE9Pj4+PkJPZN7SzczO219GOzEuKygnKS4zPE7OvLCqqKelpKeprK+1vMLQ3t/q9e/lfHPn6PNfWmFcW01HRk1RTEdGTExKRD49P0BCR1J32tHQ0tjrVkM2MS8sKiouMztL5MW1rKuppqWnqqywtrrAz93d6Ovg3Ojt4ODp6l9YW2FVR0JETFBNSEhNT0xBPT1AREZIUv3SycrP2/VTQTkxLSssLS83QmbHt6+sqKWlp6isr7O6xdLb6vf48OTe3uLk7/xeU1ROTEVCQkdOTktISktLQz07Oz0+QkpY7tfNz91qU0Q5My4sKy0xN0R5yLmuq6mnpqiqrK+2u8DJ0dbZ1dLOzNHV3ultX1FKTExOSEdES1hgWlFPTU1IPjo5Oz9GTl3mz8jJ1PpYRTw0LisrLTE6R3TJt66qqKenqKmtsrrAyNHc7Pns3NTV2eD0b2FZS0VHR0dFQkFIVV1VTkxJSUdAOzo8QUpXbuPRycrU/VNEOTMvLSwuMztN38W5r6qop6iqrK2wtr3Fys7R1dna1dPX5W5cVlVLSUhIR0dHSE5bXVlTTUlGQj06OjxASll9287Jy9d7T0A3Mi8tLS83QV3Sv7atqqmpqautr7O6wMbKz9LX2NPO0tvpbV5ZV0tJSktKSUpJUF5iV09LSEZBPDg3Oj5HUmbq0szP4GRMQDkyMC4uLzdAWs69ta6qqKipqqyusrnAyc3V3eXv6+Tk+WxnY2hbWlpbWFRRT1RZXVVOTEpIQz46OjxARk1d+9vU2OxeTkI6MjAuLi40PUvryLuzraqqqqqsrrG3vcTIzNPX2NnW0tbk8nx9W1dXUk9NTk1VWlpTT1FOS0RAPDs9QEZOZefX0dTlYk9DODEvLSwtMjlFbMq6r6upqKioqq2xuL3Cytjl6OHk6vV+c/1kU1VUU0tJRktVWVJNT1JSTUZAPkBBQ0hSdt3V1tvqZkw9NDAuLCstMjpJ8Mm7r6qoqKipq6yvtry/x87V19fT1dfe3/xqcVxTSkpITldWUFRgXlxTTUhHRkJDSlpx6N7i62xUQzsxLi0rKi01PE3gw7mvq6qqqquvsba9w8bN3dvU0tjP0dHa4+RhW05MR0xTU1JUWlZZUEtFRUZDRU1bbOTd5vVeTT01MC0qKCstMDhIdMS2r66rqKqsr7O4u77HysfBxMXDwr/KzdPqVklEQEdOUUxi+vhwZ1hRV09MTmfw39ve62hRQjUuLSkmJiktNT5T1Lmvra2sq6ywt72/wMbMzMW+vb++vsfCzP1OS0NAR0tITGhjampeTk1NSUhPXmzm3+T/Xkk8My4sKScoLDE7SfbGtrCvr6+usLe9v7++vr68trS1t7e/wcLbTkhCPEdTT1Hx83R+W0hFRD5BSVrr0M7Q2HRLOzIsKSclJysxPl3Vv7Wwr7CzuLq+xsnDwby4t7OxtLm4xNLOdEdFRz5Ne+7fy9l0dU0+Ozo4QE1k07++v8psRDgtKCUkJCctN07Rvbi0tLa5vcfQ2NHKwbq1sa+ur7O7v81q7HxPSlFLYc/N0M3aX1RCODQ1Nz9T6Me7ur7KXjwwKyYkIiUqM0brzL61s7W5vsbCwcLBvLeyr6+wsrS5wtTcfffoaVdodmnk3eTp6lRHPzo3ODk+TXDTxcPM3FU7LywoJSUnKjJBV9nGv726u7y+vr25trOysbCwsLO3vcHGyNrg3ed1/m1ib3pnWFlORz47NzY3PEJNa9/Y2VxLQjUtKyopLC4wOEty0Ma/u7a1t7i6uLi4ubi2s7Gztri4urzH1Nvh+GdeWmf87vD1a1hNRD47Ojo9P0lSVE5PS0ZAOjYzNDQ2NjxGXOjSysK9vLu8vLy6urm3tra2tbW2uLq+xs3U4fh4ZV5fWlNSTUtKR0ZCPjw8PTw7PD1ARkpMTlFXWFVUUlNVVl5udurWzs3JxsHAv769vb28vb29vsDBxcnN0Njj9W1fVlBNSkdGRUZHQ0JERUREREVGSElKTU5RWl9dYWz86uPf29XS0tLPzczNzMzMy8nOzsvO1tPS19jZ6unj7+jz+2RqbWRUUVNaVk5UVltaXFtqY11bW2ZkXF1wb3Rne9faev3f6dzi7vrf6dvU28/Q4HTWz+j43P7u3/nrcmFeePD4aVdYbntkZlJPYfxoXVVXXGFocvVuZmnw5u5kWGtxcGdtaXbq3eDh3Nva3dnf9HVz8/BgYvVwX2338fvpeXni8F97Z198e3t8Z3fb4u7y8OPn5+/w7fhx6e97fvr79m5uempwcGt93udp/Nvc5On16+ni6+/f6fHg3Oz0dP91aFtcWl9nW151fm7x7P59e25vdHh8bG19//Pl3+pmfd3l82xo8HtkZ15t+m5Xbuh2X2z6dm1sem32+PF48Obq/u1xYWBweHNqc/rw5+fs9vl67PRvZvr49mlse/Xv+fz55OTk3+Tq3uZy9etxaPlvZGT17Gdje/T07/f8eOXkfvvu73F2921jcH778vzv+HPq6H5tbm1tbWxkbmJeZ3ZrbmFeenhoe/Fsdu3o+d/g8ffh4O/9cuTubu7wV2na5XFe4tjU4X30z+FmW/bzXE9aW15XVWR9ffZs+fL31eJncO/t4mpi/X5qZm705+5lb9vkatzhavLc+Gzd7GHi31bnznFq2HFn8d9tY2xqdtxvYu/m3HBv7/dxe2dvb2b7+G31d/j0//ne7O7v6O3q7Wlz4PFWZWVqX2ZdX2VgXH3ubnrta/DvfW5rZWp3ZGn0Zmnubl15fXR+9/L+3994deDu+Hzs7W3y39306Ofb4Onk6+3j4mh16+tm/mdt7PRsd25r8X5w/HJra/79cGRv9fLt8u379f3ffW1seG11d33tfejl4/fs/uj6bXz7bWd9ZWV1fWVsZGdw9fprZWVte2lkYF1iZ2pfZF5hY2xxeXv98+7w7+js6/R47vXw5+775+Pw7Ozo6Ojq6+zt8Pjy6ODr6+jj4uHn7u339fpuZHV5bGVnaWdhYmdnX2FsbW1wc3BzfPDv7/Pr6vT17ezw/HP69/Lu7vTv7e7z9vLx8fDu9fTy+n15d2xoZ2hnZ2dobGxsbHV8ef7t6uzt7+3w9vv8+v329/758fDw9P339fV+cWlpbmtsa2NjaWhnaWlnbXV1cG54/P56dXJ0efz07+nl5N/c3d3d4OLh5+vm5+ru8PT9/Ph+dXJyc3X9/25mYl9hYl9eX2FhYmNnZmhsb3J0en317e33dXBqZmdsbX3/cW30fmt3ysvcXVRjxbe4x1Y+OkBR4NXJv7WurLHFTTQuLS8wNTY5PkpRW1VLR0le3cfAu7i0srK1ub3CyMzKzMzO0dfeRknfd0tAPUu9srLNPi0rLTVBSWfFsqypsdA7LSgpKiwuMDdATE9MQENJVWvRwLWuq6yxuL2+vbi2tbWzsrCxucn3UEpNT09OUF5fSVLtSzYuLTjnxs9KNS8zOkFGRmLDsa2uvWQ8My8uLzA1PEZPTUZBPUhWe+jMvrGrq665vr+5uLe6urexr7K5xtxxaFZRT1dbYlxURjpGXUk3LzJJxb3VPjAvOT9JSk/cua6utcxTPzw3Mi4wN0BLST46PEdYYOPQwbuzsK6wtbu9vby8vLu4srCwuMDU52RUR0JFTVxcTUE9NjlISjszN0nKvtBGNTM7Pz49QW69sK+1v85wTTwzLzE5RExKQj5BSlZRWOrJvbeysK+xtbq7urm7vLq2s7O3vsfS7FZHQ0dMVFRLQj48ODQ7SEI5OD9lztdPPDc7Pz8+RFbPvLe3usDM7U49NzU4PUFDQ0BDS1JWV1vhxLu3tLOwr7C0uLu7u7u7urm4ub3H0vdaTklGRURGR0ZDPz07OT1GSEA/QUped15ORUZJSUlIRk520ca/vr/CydZpTEQ+PDo6OjtAS1lt69DJxr64tbOytLS0tri7v8LDwcDEyMzS3fddUExJSEVCQEBAQEBAPz9ESENARElPWlhTVFZbXlxbXFxt5tjNx8PCw8jO3m1XTEQ/PDw9QUZNWfbazL+8vLm2trW1tre5ury/wMLHycnQ2+V0YFdNSUVBQD89PT08PDw8PkE/P0NHTVVVVVleZmleW1pZYv7m1cvHxMTIzdbqZVFHQDw7PT9DSE5bbdnGwb+7uLi2tri5ubm6vL29wMPGzNPX5HpjVU5LSEVEQUA/Pz9BQkVHTUxISU5WYGdoanB6fm1lYl5bYG/w2c3JycvP2ONwV0pCPj09PkFHTll23s/Evr26uLi2tre4ubq8vcDCxsrN1N7l/F5VTUhFQ0A/Pz8/Pj9AQkNESExKSExRWGdtZ2lrY2FeXFtZWV5v5tLLycrM0Njhc1tPSURDQ0ZLUVxy5dLKxb66urq3t7e3uLq7vb7BxsrO2ux0XlhVTklGQ0FBQD8/P0BCQkRHSUlLTFFZWFZfZmzx6PD0fWplX1taWVpn8drLxsbHyc7W425YTUdEQkNGSk5YcuDPyMK+vLq5ubm6u7y+wMLGyc3R19vk+GxgWFFNTEtKSUdGRUVERUZGR0dHSElJTldTUVpaXHF+c31yYmRiXV5fXWRz6NXMysjKztXe+2BUTEhFREdNU1/+49XJw768u7u6uru7vL/BxMfKzdLX4/hyZVxYVE9NTEpJR0VFRkVFRURFR0lLTE9WWFVbYWT/5unp6PhxcmpiYWBibPjcz8vIyMvP1eJyW09JRkVGR0tQWWjv2tDLxcK/vr29vb2+wcHFyMfL09nh+2xhWVZUUFFSUVVUUE9NSUdGQ0JDQ0NFR0lMV2Jqcvz76d3c3Nvk83luZ2ZfXmZ+49fQzczN0dnkeF9UTElHR0hLUFts7dzPycTAvr6+vLy9vr/BwcPHys3V3eh6amddWllXV1hVUk9LSEdFQ0FAP0BAQ0ZJS09YW2BtcPjo6ezo7n1tZF1bWVpdZ/ng18/MzM7Q1uD1aFpVUE5RVVljeevaz8rGwsC/vr6+vsDDxMbJys3R1+F+aVxTUE1LTExMTU1LSUZDQkE/QEBBQ0VHTE9UYW958+3t4t7e3Nvk6+30+nhtdPbq3NPOysnKzNDb7HJcUU1KSUpNU1xp7trOycTCwL+/v7/Bw8XHyszP0tfe6PZuYltXVFJRUFFQT05OTU1MTEtLS0tLTE1OU1hZWl9pcHf38PL5fnNrZWNkY2d75dzV0M/R1t3tb19XUk5NT1NZYHbl18/LxcC/v7+/wMPExcrO1Nne5u3+bWhlXVhVUE5NTEtLS0pJS0tLTVFSUVRXWFtfZmdnbW1xev3+dG5raWhnaXH+6dzVz83Mzc7P1uDzb2BWUVFUV1tn+eTZz8rGw8DAwMHBwsPFyMvO0tjd5vxmXFdRTk5NTExNTEtLTExMS0tKSUlJSUlJS01QVVtjcH77+v95cG1scH3u5dzW0c/Pz9HU2N3l7vp1bnFsde/p5dvSzs3KycjIyMjJy8zNztDV297m+21gWlZTUE9OTU1NTU1OTk5NTU5NTk1MS0xNT1JXW19ocv3w6ODe3NrY19XU1dXW19na3N3g4+Li4N3Z19TQz87Nzc3Oz9DQ1tnZ293e5Or2eG5pZF9dWldTUE9OTU1LSUhISEhJSkxNUFRYXF5kaW5zc3v68uzo4d7d3NrZ2tjX1tXV0dHQz8/Pz9DS09PU19ja3d/j6Ovx/P92bGdjXVhVVVNRT09OTk5OTk9QUFFTVlpbX2NnbXn58Orq6ejo6ufm5+Xm5ubk5OTh3tvZ2NfV1dTT0tPV19fR3tTd0+nb49zl+37vXe5dXlxVYmJYXVtSfFRhUVRWWVFYTlpZWl1YYGplZ21t7nj08Ozl5e3m6enp7+/u8vDy9/by7+/t5ufk4+Th5enm5uPl6urp5+fq7PD1/H35/fv8+312c25samlnZ2BgYWJmamxsbW56fXt5+/r08uvp5+jm4t/e3dzf4OTo7vP4eG1oZWlpb3RxcnV2cW5tbnBwd3Z89vb49PH49/d3bnBxcnV0bWtsbmxtbnF3fPny7u/4/3h4enJwcnV2eHZ2cm9tbGtpbn3u6+zt6+rs7fJ9bm1sbW1ra2xubW5ub25wbnB1fvXy7+3v7/P07/T8dm5vdHZzef767+ro5uXk5ujm6Orr7vDx8e7s6Obn6+7t7ejo6Ovu8Pb+/H5+fX18e339/f97dndycnRzcG5sbGxrbW5ub29ta21vc3RwbmxucHN6eHBsbHBrbW5uamdtfF/u4vxWX3TWz9xhT1Zo3tPT19PQz9f7VEU/P0RRdNzOyMK/vsDJ2HNWTUtJSUlNU2Xu2tLNzc/T2uT4alxUUU9QVFxhdPrq5ebp/m9hXVpbXWZu+/Dr5uPg3+Pn5+fueW5uam168e7p6O/r6+v0+XV2efv89P1yb2tnZmhkZWx5+/Hq6eTf4efm5+zv7fZ+/PT19/Py6un0cGtmbPz1dXBvbn7r5+vx/Xf66uv7c2xucHH9/XVvdH7s5On4bmZmcff2d2lfXmJnaGNgXF1fZGxvaF1cX2h29/Z4amhx9+7q7npzd/bs5ef2dnJ3/eni5+3y7ebf29rd5unj4Nza2t7k3tvW1Nbd5uzx+n1wbnFxe/L6bWNYUVBUV1teXWBpbXh3Y1hQTElKTVFVWVxm8+TZ1dPQzcnIx8jLzcbCxsrU3tjNyszaWkQ8OT1JZdfJxsbI0PBOPDEsKSkrLzc+RVjz2s3JycvIycfCvrq2s7GwsLGzt7vAzNnvaVxXVFNVV1xeW1RQU1JKQz4+Qk9ke19IOzQyNT1W0r+6t7m9ye9NOzIuLS8yO0Bb4drO08zLxsPAv725trGurayusLa8wMbL1N90ZF5ZVk5LS0dKTlZRST88Pkdb8fRdQzcxLzQ+W86+ubm8yOxLOzQvLi4xNz1HU+nd0tTb1tXMx8C8uLazsbCvsLK1vL/GzdLe6XFkW1hUVllXXF5jV0tDQENOX/H7VkA3MTE2Qn3GurW2vMtjQjcwLy8wNDg+RlBh7trOysjGxsK+urezsbCxs7e6vsPIzdLY3eb3ZVdPTExKTVNfXE5HQEJJWXJ1XUg7NDAyOk7Yvre0tr/XTzw1MTI0ODw/R0xZaN7OycXGxsTBvbm0sa+wsrW5vMDDx8rLz9Pd625aU09OUFZiW01DPDs8Q0pNST84Mi8xOEb3w7m0tLvMWT40Ly8xNDc8PkNIVXDYysXCwL+8ubSwrq6ur7G1ub3BxMjKzM/Y5mlZTk5NTlRcX1FIQEBCSlFVTkQ7NTIzOENqy7y4t73PXkE5NDMzNTg6PT5GT2rZzcfEwL25tLGvrq6vsra6vsTIycvN0djrY1ZMSElLTVlmalRKQT5ASE9TS0I7NjQ2O0hxzb+7ur7LcUg7NjMzNTY4Oz5FUXjZzMfGw8C8t7Owr6+ws7a5vL6/wcLDxcrQ5GRWUVVZWWVhbVNKQj9CR01MQzw2MzI2PUz1zsK+vsLOfk1AOjY2NTU2ODs/Slv63NjQyL22sa6trq6wsra4u73Aw8TEx8vU4HNdVVdTU1paXExEPj5DSExMQTs1MzQ4Pklb7dfNyczXdk9BPDc2Njc5PEJKU2b95tXJwLq1sa+urq6wsra6vL2+wMHCx83Z7GVZWlpSVlRWT0dCP0FISkdAOzc2NztASFNj6tHKydD0UkQ8ODc3Nzg7P0ZOXf3dzsfAvLeyr66ur7K1uLq9v8LFyMnKztng7vHxbV5dUkxHPz4+QUNCPTo3Njc5PD5ETmnby8rO4WpNQz88Ojg4Oj1CTFr92s/IwLu2srCvsLGys7W4vL/DxcbHyM3S2ePr9mxka2FeU0tIR0dIRkE+PDs7Ojo7PEJOZePZ2tfqZFZJQT07Ojs9QklPZuzUxr66trOwr6+vsLO0t7q8v8PEyMrQ297qe2ZYVVdQTUpDQT8+Pz49PD09PD89OTpATFNYYV5x29nrblFJSEdHRT8/QkdTc+HPxL23s7GvsLGytbe5ury/w8jLzdLc6vtpYWZdV1ZPSklHRkZEQ0E+Pj08Ozs6PD9FTVls+Ovs/mhaUkxIRkVGSlBf9dXJv7u4tbW0tLW2uLm8vb7CxMXHysvP2N7pal1bVE1JRkNBQURDQUJBPj08Ozs8PD1ARkxYb/Pt8fZyX1ROS0tMUFhm7dfMxb+9ure3t7e4uru8vr/AwsXGyczN0trm+mxeV09MSUVBPz48PDs7Ozs8PDw+QENGS09UW19ja3J+dnBraWxse/vt3tbPycbBvry7u7q6urq8vb2+wcXJzc7W3e57YF5UTkxKR0VDQUJBPz8/Pj8/Pj4/QEJER0lOUFdbZm73697c2NPQz87My8jIyMXGxcTDxMPExsfHyMvMz9LV39zn8W9iXllRT1FOUUtNSktLTU1NS01OTVFOVlNeVlxbW2BUaFJpWm9g6Xbn6+Xb29XdztzM3tDXz9bQ29jX3+Pp3vrg5/j07uzm9Oh4ff5zd/z3aWpmemP0bmt1+V75YOxbamtcYV/9XfRn+m5v+mvqfejp5Ofs4uLd6vNm4mDlVONW01Pccuhr2+hw93DnZOZc3lrUamdi2VprbmB29u5e3V7XXeRf0VPnamtnfVhfe3Ri7Wbc5evy++576Vlqaf/tbP112V3tWOv/bF1p/lzfXHtd5mXtWGl4+XfxZufa4eHt5+7gaeF83XnrYut75Xvr/db78u7h7nxxZ99ifm7sbNpmdl75XHdbamfoe/Pr6dne2eTaeOV533b3ZOft6/9ubW98Ym3+7W3x/Olwe2Dt6/9nXmh6dVlpZG5oZmV1fWReYXx1/Gd27+t2e/rk3+tybWthbV9jaen+7+fh39zwYm9lcnh1YPXm5+3l9eDl82xdZ3htW2936efY4OTe/Gn8+2fv+Pjr6Hzn5nnx9W58fm56a27t7mrz5eTe6G1t5ed9X2Jw6Pxib+7j711eeerxZV936/JkXP7o6mpbZuLsdV9t++rraW575fDseN/Y5nj8++nj/3H+6en/Xmrw3Xtta3Xy6HdmefLp8nVZaHz3/mFbZl1ffXf54uv4/PXg5Xdu/n38a27q/vBtVVNwa2RlW1xncfrv7N3p/Onh5d/i+eHb2NbX4enqfXpiafXf4+br6ePs+F9kdHF18XJdXlxWUltq/PTr7Hx98P1oZnvy8XtrXW7i4Or+7eTf6nRp+9za7Wxu6uD+d/3w5+Hvb2756/dtZXDq5PdqbXl7cFxcdf76/V5ZaHrr7G5kb/Ph43lgZW10bF5gbnh8d2/56OHwePLs7Ox8bPXo6u/28n1pX15eb35kWl1r+PN0aHHt3NvncXTo4+DramR+7H1kX1pcZ31zdH7w8e7q7/j17ebd2trd4O34dXH97vN3bWx37uvl3d3c2dzc1tPZ5et76dzz4+dXbeZpbvRXX2xbXG1jYXBmZGtoaHNvbfd6bG1mZW9qYmBhY2lrZ2hfXmZoa3t9cfXq8PH0e/v1+/jx/ffo6+vp6+/o6/P16e7u497d2dvh+XBqc/19bmReWVxaYWVqZFxYVVxmcvnp6d7a2dzpaWFiZW1fXF1dYP3s6unj28/Jx8nMy8jJy8zP0M/DyuJSTlbOv8R5PDAuMz5b8NfNwsHD1005MTE3P0ZNQD9ESktSV2DSysTJx8e8uba2ub29u7y7wMXP1N/c7mtUSkRGTlzxe2VcRUVgakxDOjxkyMHYQC0qLDldz8bBwb+8wNdKODM3P1NrYU5JSVda19PX4dXMvLKxsru/wbu6tbi6vby+wMfV6lxSSktJTUxNS01NTUtKQT1EWFpOQDxCeMvIdT0vLTJD783ExMC8ubzNTjgxMzxPdWtYSEVKXejR0t/bzr62sbW5wMPAu7i4vMDDxMDAxtxdRT8/RE1aXVlXT1NPTEg+OkZpcGdHPUFuz8foQTEtLz/hwbm5ury6vspbOzEvNURfemZMRkda6tTMzM3Nwbu0srO7w8vIv7u5vcLIxsbG0XVLPz1ASFJfWlJKSEZEQ0Q+PlJzbFVGPkr7zcx7PzEtLz79wri2uLi6v9JOOTAwN0h72+xUSkdc2snIzNbRwbmxsLS8wsW/u7e4vMDGxcbI2V9FPTs+R1BcXlhOSkhHRUE7Pk5ZWUw/Pkx7ztBePTEtMUHuv7e1t7e7wd1HNi4tMj5T+vRcT1BV4s/Mzc/OwLexrrG4v8PBu7i4u7/GxsXHznNKPj0/TWP0+WFRTU9OUEs+RFNcXVZGSFzqz9hVOy8sLzxjw7e1tbe8x3tCNC0sLzhHZvNuX19w1dbR0NfUyL23r6+xub/KycrEwMLCwsLGy+1VQTw9RE5s63FdT0lHSkdHPD9MWVtYSEVb6srL6kU3LzI9W8e5tbW0ub3NWzswKywxPE5x6ebf2s/P0d9+8NrHu7a1tLm9wcbLy8vLx8XBwcPO4lpMRkdLVWJrZ1xTTktKS0lKPklUZltZRUda58rL3ko7MjY9WM+9uba2ub3SUzkuKiwwPE/64NjX08vNzdXs59jLwLu7ubq8vr7CxsvRzczHxsbQ3mZWUE9OUE5LTEtLS0pIREY+QVJgYFhLRFrox8fUUD0yMjlH3cC4tbK1uMPtQTQtLjI8TnHb1dDSzMzO2/xl99LFvLu6u7u9vb/Bx83V1dPSz9fkY1RMTExQUU5OTUxMTEhGREdJQFBgcWBjS1rpz8bObUM6MzpBacu8ubS0t7zOVzwyLjA3RWPf29rg3drl6WdlYt3Mvbq5vL/DxMDAv8fO29nUysvO5WBPTU5QWFFQSkpGSEVFQkJES1RUX2llVVBMVfjVzttbQzs3PUrsxrq1srO3v9xLOzQyNz5ObuLY19vc3+r4enzr08u8ubm7vcTAv7/AyM/Y2tXJzc7gcFRTT1VYU05JR0dMTE9KSEZHSlJUVFJPWFts8OL3bVxZX3Ls4uV0X1VVWV5mdnv66OHl82lWTEpMUFx66Ojn6OTe3trY2dXOysfGysPK3L68vMjjSfbLt7O+ajksKzZHybq0tbO3ucpPMygkJS06XXx1TkVASk5ZV0xPX9K+t7m7x9bZzsa/v8PGzMnGx87jVUhGS2Hg0tPeb1tQTUtJR0lLUmN4/XNhV1NTXGJkZGFdX2t97+vu8e/p39zZ2dvd3t3Z1tfZ2djX2Nfi3uTr/f117ePo8GxjXWN13tzd72tbWFVSUVFWW2z55+76Y11dX2V1//7z8efj4OXr/Xl1fPTu7PP+/vv/eWxoYGNjbHFxamFfX19fY15eX2dt/Pbr5ubr8/Do397c39/f3dzb2tnd4uTi4OLi5OPk4Of2dW9vbW1oZmtuaWpoY11cX19fW1lUU1RbZ33w7e3n5+ns9XVobXT57ufm6ezp4NnU0tjd4lXx0M3WekpQ5MW6wuxAMy86Tc+9uby9wcjXTzsvLC04SOzZ7llJR09n6dvq7t7Nv7q6vsfR0crBvLu+wMbFysrL5lJFQklx39PxVUVCQUpPTk5KS0xSUlFGQD5CTGL/+WdWVVhhbPt19/Le083MzdTc3d7Tz9HW2+Dc19bZ6WpcXm3s49/g7/v+/fTt6+rr5t/d3+b2c3N67ePg4uTr6vD39Ozp5uPk5ujxeG9xen7t7vb3dPtqXVBOTVpt/m1XS0VDSFNf+/XzcmpZUklCPjw/SFdk/mpqa+7c2dfY2tfLxb68u7/CyMvQ2N3p6efa3NTj4WRKdvPgeG5Ke9vCwNdKNi0sN0jMvLW3uL/D3U06Ly0vO03Y0NDtW1Nde+bZ6N3Txbu2t7m/yMrIwb29v8LD0MPGz35RRERNVHRZVEZDPkVFS0pGREJHSExERD4/RVBdaV5VVVFfav349Wv15NvR09vU193a3+ne2dXX429USkpPXuPOyMTFzNj8T0hGR09d7NfN09Po8nzu3tDKyMTFwcPCxsrX4Orl3NnZ2t/q9PBbT3BiZk9LRF7gyMjmSjYuLTM9dcu+vbzAyehJOC4rKy83R17y4/z79OPd3eHcz8a7t7Oztbq8wcK/wsPLztPLzs7jXk1DRUxi8NnifVZMRURBQEFBSE5aYWJXU1NRWV5ocXNnbfzp2Nre09fU2Nzs3NvV1/BgTUZFTmHWxL28vcHJ1WxOQTs5Oj5JXe3f0dXT29ri2trZ09LKyMLCwcPEx8rO19/t/GphWVZVVVZYWVpaWVlZVE5NQ0pNVFdWT1Nkd997WkU6NTU6Ql/Ww725ubvD2lQ+NzExNDpEV+XNwLy5ur3Ayc3Pysa/vLy2tra5vMLFydLXbVpNSEdNV3Lj4+H4blpNQz06ODo9QkxWX2psb337ZmReWVZTVFhq8dzZ3O1gUktJTFZw2szFwsLEytZ4U0Y9Ozk6Pkhd3szCvby8vb/Cxs3Kzs7OzcvHwb++wcTM2e5pW1JTU1ZYXGFdW1VORkI/Pj4/QUZKT1htc/rq5+n3bV9fXF1dXVtTUE1OUF373M/Lx8jKzdbjdF5PSkVCQ0ZNXOvPxb+8urq6u7y+xMfKztHU1dbQzszN0dr3X1FKR0dHSEtMTk1MS0lGQkE/P0BCR0xRW2f+5ujd3N/j6PF+/P71+Pn9cGllZ2334dTMyMbHys3V4HVbTklFQ0NFTV3r0se/vbu6uru8vsHFy8/Z3uPp6Ofk39ze5vVuXlVOS0hGRURDREVGSEpLTU5RVFNSUlNVV11eZXB5/fju7+32+nBtaF1YU1RWX/XbzsjDwMHDyM3Y8GFSTElJSlBd/tnMxb+9u7y8vsDFys3T2N7l7fj7fHx8+fhxZl1VTUlIRkNBQkFESExPU1lcYmZmYl5cWFRWWFlcYWhpePbt6+3zb2VfXFxjdunZzsnGxcjM1eVuWlJOTE5SXf/bzMW/vry7vL3Bxs3R2N7n9v5saGpvfPLs7vlsW1BJRUA/Pj4+P0JGTFFYYWdscGtjYVtZWlpfZXL77enp5+nt9vb4cGVfX15n/+fYzsfEwsPGzNTgd11VTkxNUVlu4tHKxcG/vr6/wMTGy8/Y3un+bWdjXlpWUU5MSkhHR0hJSktNUVdbXV9fXVtcWlhXV1dVVVVXWFtdXF1cXmFka3f06eHd2NTR0M3Mzc3O0tXX2dra2tra2tva1tTT1NPS0tDPzs7P0NLX3OPr7npsYFlWU1FPTk5NTExOTk9QT05OTk5NTUxNTU5PT1BRUlNVWV5lbXj16OPe29jW1dTU1dLQz9DR0dLS0M/Q0NHR0dHR0tTV2dzd4Obm5OLo7Pd0bW1sZV1aWVlZVFJSU1dbXlxaWVdWVldYV1hZW15kamxub21qcHd4evjs7O7u8O7t7Ozu8u7q5OHi4uTi393d3t7e3d7c2tra2+Lq7PH4fXlvbGtqaWpvcG1wdXZubHBvcXNva2NeXFtcXV1dW1xjZ2dnaWtuamtscHz77+rn5ePm6Ovv9vjw9P15eXlz/vTt5+Lf4ujt8Pby6eTh4ebl4uHj4Oju8vt+d3r4+XNtbXb06+Xq7fH1/XdzbGZgXV5kanP09fr59Pj2/W5lYF9dYGVpZl9dXGFpbnJ2dnj+/+/p5+jo7vPv+XlzdXj9+vr28erm5urs7/tydvv7/3398+zn6+32fm1jZmdrbG998Oro7vn59n11em9paGViYWlzendsaWtzdXFrY2BjbvHo5ebm6efg29nd6fp6fO3l5Ojm4+Xj3Nnd5n5qaG17fntxbmtyfPfx+Pl9dvzu6u37cGxsfvDn5ODh5+bw9vn9c21nY2Zna2tzb2NiY2VobGhoamtuamprcHp9fXR2ffDu+HlycP78dnRxeXFua3BtcHNwc3Bram54+/Tv7vby8/x8c3359fb3fvfw7O7u7u7v8+zq6Ofh4+bk5+ju9np3c251b3R0ff39+nxya2tpcf3u7Ozs7vPt6Ojn7v//8vDt7O3v7/N3bmhqanV4cnj28/ny9/5+/3BtbGhkXl5hZWhpamxxbnVudG9z//3z8uvt5+bm6Ojue3BqcHR+fP32+vh9d2ZfXFtcXWNpbW5wb3F2dv15enn57Obk6ez38Orh397e4OXl5ebj6vZuaGZpcvzv9vb17urp7f1rY19hbf38/fv/+f788fD4d3Zz/e7o6fB+c3BubW5vbm1oZ2xvdnt4dG5ubXj66+rk4d7d3Nzf4+jq8/x3enz68/Ly/H1xamJhZGZuePbt6vH+cGtnZ21nbWxraGhrbXp+9vz++fPr6OLg4unx/nNoZmZkZWlwcXv9ff93cHBta2xzfH39/nZua2ZnaWdmZ2dqa25wc3p+8O7r8PT4+vb4+nt2bm5wd/z16OTj5OXn6ujp5+fl6/Dx7+jg29zb3t7f6e739PHt6OPh4eXm5uzv+f15eXNtamRfX19janBvaGdmZmRmZGNjYmNmamxudXZwc3ZubGhmaW1yfPPu8fj7fn5+fnpzcnF2c/728vb2+Xl4fX7/fHlzaWpscHn4+e/t7fD5/Pt3c3Rvdnr06+nm4+jze3Nva2xtcn3z5+Lf3t/f4OHi4eTo6+zv9u7q6erwfXBwb2dye/Z2bGBeZXL0/WxZTklHS1f+2MvJyMvR4GtUSEE+PkBGT2Ll0cnGxMTHyc3P09HRzsvLyMrJy83T2u5pWU5NS0tNT1JVVllYVVJMR0NAQUNJUWD65uTl7nhlXVpYVFNbYG519ufXzMXCxMvdZU9MT17gyr+7uLm8xNB4TkI9Ojs+Rlfvz8bAv7/CyMvR0dXT0c7LycbHyMvQ3nVeUk5MTEtKSEdKTFBWW1lUT0tGQ0NCREZKTE5WWVhqfeXq+nJt8tvPzc7oXUtERUto0sC5tbW3vcroT0A6NzY4PUZW58vBvLy+wszS2djTzMbAvLu4ubm8wMnZ9GBYU1RSV1laXl5oYF5YT0pGQD8+PkBCREVGR0lLT1NWW1hUVFFWWVxjXPXr2tzb3NnRzcvP0O1jTkhLUXnSwry4uLm+y+ZSRT06OTxATnnPwr25ubu+xcvYz9XLy8bAv7u9vMLE0Nx1W1JKTEpMTVBQU1JOS0M/PDs7Oz5AR05XUFpaXV1UV1Bce9rPzNd2UENBQ07zy723tba7xdtRQDkzMjI2PEVb28m+u7u7vcDExsXCv727ube1t7a7vsfX6FpaTk9LTUhJSUpVU19dWE5IQT49PD0+QUNFSUtQW2hm+evb3e/2Z/nezMbEyt5aRUFATfTIurOvsLS8y2RGOzY0Mzc7RFjfysG+vr/EyMvNy8nGw7++vL26u73AzdZ4a1dTSUhGRktOX2JwYVVLRT89PDs9PUBCRUlISkpQUFRdcux2cF5g/tvOy8rVeE5DPkBMfsm7s6+vtbzNYkY7NjQ0NztEVuvPw7++v8LFyMfFwL68uLi2ubi8vL/DytfpWlRMTk5VXGRwd3xgWUtEPjw8PD5BRkhLS0pKSktOTVBcbPZw/m354NHNy9LyUkM/PUZe07+3srO3v9BbQzo0MzM3O0FObtrLxsTEyMnNz9DMysPBvr2+vcDAyM3h+F1VVU9TUVpaX2ZucmleUUpDQD4+P0JJTFJWW1tcX2Fv+3ju7eLn9/h06tzOzMzcbE5FQURP/c2+uLW1ucDTXUc9Ojo8QEdPXuvWy8fGxsnJzMvKycXDwsLAwsTLzdXZ3eTscV5TT01QUlhfYmRcVU1JRkNDRUdKTU5PTk1NTlBUWV1iXGBeam9z/3ve1cvLy9XrXU5LS1V308W9u7u9x9xcSkA+PkBGTlx+29PKycfJy83S1tjVzsrHxMTFxsnN0djf8XBmXVlUTkxMTU5PT1BPTktLSk1LTlNcYlleV2Rm7urp/19OREA/RU/4zcC8u73E0mtOQ0BAQkpRYHzf1s7LysvOzs/NzcnFwb++vr6/xcjN0tne4Ofs+W1fWFFPTk1NS0xLSktMTU1OUFJRUVBOTk5QVllhamxnZGZjY2JiZmpwd3p8/np+/HBtamZnZWdqdfnw6OTf3NnW19fU0tLRzs3MzMzNz9Ta4PN6fe3p6+no4d7Z19rleF1STk9XbN/QycbI0PBVRj47OjxASVJj/erj4OTk4t7b2NPQzs7Nzc7O0NXc4Ont7vP4/XhtaGRfW1dXV1ZWVlZUUE5NTU9UWVxcW1paWlxjbXrx6+zs7O3u8e7p5+To5+rs7u7o4d3a2tvc3dvc3uDj4uHj5uTl5eLk4t7c2tnZ2tzd3+Dl7PH08/Ly+XdpXlZST09PU1dZXF5fYF5eXVtaWltcXF5fYWRqbm9sbnL+cvfm3d/j5evd1cvLyc7Y9GVdXnvczMS+vb7H2ltFPTk5Oz5BREdHR0dITFRl7tzRzMnHyMjHyMjIyMnLzM3Q0tXY3ODk7PT8fX16c21pY15aVU9NTU1PUFBQUFFUWFpcX2ZtcHVydXd99e3m3t7g4ufw9/r28Ozp6+vr7e3u93twbXB99u7o5+Xh39/f397e4eDk6/Z2amluc3h8+fr7e3BsaWdnaGlsbm9zfHx1dHRvbXR2c3VycHh+/Pf5eXVzbnB2e3Rxc3X/7+vs7evq8PH3/X57dnR1eXd4bmhnZWVoaWlqbnr27uvl4+Tk4+Xo6+/5d3Bra2xtbW5sbnR++e/q6uro6Ort7u77dXFycG1samVnZ2ZnamtmZWNhZmlqb3V89e7r6uXm5eDf39/e3uDj6e7y8vP39vDu7/l0bmxsa2hlZF9iaW5wc/317/T5+fPs6efj39/e3t7d3N3i5+ns8Pj09vr8+v52bWlmZGFfXVxbWVlaWVtcXF1eX2FqdHZ8fvrv6N/b2trb2t3g5Oru+H5za2loZ2VkYWFjZWdkX11eXl9iZWVpbHB8/vv59vr69vLw9fr5+O/r6ePh4N/f3tva293f4uns7u/3/n14c3FubG1sa2xyfvHr6+jl5OPl5unp6+zv7O34/f19c3V3d3h9dHh+/n58fHVzcnr9+Pt9fPx9eHZvb3Bydnf++3x5d3p+e3VwbWprbG1sbGttcG5ubnB0e3t6cm52d3BucG5ucHN1dXp+fnx2cGxrbW9ua2dqa250fX757+3s6ufo7PD09vl5bmhlZmhvcnf9/f757+zr7e/w7u3r6+zt7u7w8fX4fXZybm5vdnr99vPt6eXk5OHi4uHh4+Xo6uvs7vH3+vj37+/x7/L39vb4/3BqaWxpaWhiZGVmZmdqaWhrbHB6fXl1d33+/3p1cXFxcnBxdHFtbm5sbG1ucHV3eXl8+357fHp1bm5uamhqb3R3env/9/Xv7evn5ebk4uLg4ufs8vl+dnZ5dHR3fP35+fj1/X3++/z8+Pf4/Xx6/Pn5/P5+/vz69vLv8PHw7urn6Ors7Ozv+nlxb3BxcXh2b290/vjx7/D3+/n08O7v8vT18+3r6uvu8/j9e3p2b2xqbG1rampqbW9xcnNydnBwdHV2dnZvbW5vcnZ6e3d2eHd+/Xt8d3p7eXl3cW5tbGloZmNlZWhrbW93/fz7/ffw6+/z9f35+v5zb29udHr98/Dv7efh397e29vb3N7g5unr9nt6eHt9fXd0eXl8d3h7dXBub25yd29ra291dnF3e/jy8O/w7vH29fHx8fH4eG1sbGtsa2lscn7y7evp6urr7Orn6enn6Ors7vTx7/H4/nh4/vf8eXt9eHv7/3l7/H59/3Vwc25qam1taGdnZWdpaWhnaGZlY2ZscnFtbW1ubm54/fjy7ezt6uns8X51c21sbm1qaWtta21uc3v8+fPx8e/x8/X49/Dt6enq7O7t6+jq8fj6//317u/x8O7t6ujp5+bn6Ofm5uzv6+zt7/X5+fn++fPv7+7w9vl+ff37/XhybmxrbXFvbW1ubW5wbGhlZWRhX2BhYmNiZGRhYmZnZ2twffn18e7u8Ozq7O3v8/X2+v7+eXj++/bz7ujp7fL4+vt9cmxqZmFhYWBeXmBla211fPz07enp5+fl4eHl5eXo5+nt9fv9fv95dnV0d3r+//vx8Onn5+Xo6Ovv8vDu8PX5/X7/+vv8/fz7+Pb38O7u7Ovu8vp2cGtpZ2dlZGVmZmpwd3h0c3JzeX38+fr++fbv7e72/XRtaWZmZGVkZ2tubm95eXl6++/s7fH9c3BvamVjY2Jma250+/Ho5ebm5efq6+7u8fX18/Tz8/Lw+Pv59vPy9X59fnt+/f5+fnx3cG1tb3V+e3d0bm9xbW1ucnn+9fDs5eLh4eTm5eXk5ufo6err7O7u8fX8/v97d3FxdXV5/P18dnJycnBubGxtcHRvcHx7c25qaWhpaWtubmxudHZ6/ffy7u3u7/Tz/Hl0b21tbWxtb3R3ev3v7vDt6unq6uzw8vn49vv9/v59ff1+e3dvbm9tampqa2xtbHB5ent9//759vv9fP99fnx3dnR1eHx7ffr1+vf19fTw7/T0+Xp3eHd+/3x+8+vs6ODf4ePk5+fp7O3t8Pb29vT19/b09fT09fPv9v53bWxsamdkY2VqbW1udX349vPw8fj7/Xlvcnv8fHJwa2lqbG1sa2pqa2tw/Pv69vP4ff15fXx9+ff9/f358fLx9PH1/f53cnF3fXp4cXFwcnt5cm5vbWttbm1tc//8fv38/nt1en5+fXp7fn789e/u7u7p4t/c3N7f4OLl6PD8e3ZvamloaGdpbHB2evv18O7t6Obl5OLg4uTh4+jt83xxbmtnYmBfYGNlYmBeXFxdX2RlY2dub2xtbWpscHZ1c3b89e/t7evq5ODi4eDh5Obj5OXq7fD6+Pf5+nlvaWdpamtpaWtucHF2ev308PXx7+7y+Xtub3NwamhnZ2pvc3Fvdfz07+rl5eXj4uDi5ubm6/p7eHh1cnBsbW5ubG1vc337/fz18ezr6ejq7u/x+nt5enVzcW5pZmZnam94dnR2cXJ3efv39vl6ef73+Pn6/X199/Dr6+np7vDs6u37fXt4c21sa25wd/rx6+vq5ubl6Ovs7/R8b21rbnZ5d3BraWVhY2VkZmttbG10dXR0dHRsbGxpaWhra295end1cG9yffv28PPw7erq6OXi4N/g4ePk4+Xq7u7x9u7p5uHf4OLo7fP/dm5pZWFfYGRobXB5e3t3env9+/n07/L07/bz7u/7fHp3dnZ7fHp1bWxtbXBwamdvdnh59PH29vf5/fr6/n55e3t2cG5vbnF5fXp0eXh9/vz2+3pzbWpoa25tb3FydX728fDw7+7s7e/v7Ozu8/5+fnx8/v/77+ru8PP9d3N0dXFubXBvbnB1d3h0bmxub29wdHh7fHv9/Pz58+/q5OTk4+Dp6erl8+Pr9+3r2+Lc7+/27+7u6XxuXVpYW2Vy9Ovg3+Dl6+7+cGdgXFpZW1tbXV9kaG1zefn28PP5d3BucHp5enRyePnu6unq7O3y9/b38fHs6ujo7PL8/X58/nt0c3Z9eHv8e3Z2dW9rbG1ye3d0dnl9+vl8dXN3cHr49/5++fr88evq6enr5+Lh4uXn6/H19fj9e3hzcnV7e3VxcnV2/vPw+HZ6/ndubW1nYGFkY2NjaGdoaGptdX7+/Hx5e/z7e3hzb21uc3Nz/u/r7Orm4+Li4eTj4eDg3t3e4eHk5eTp7vx8fXp9/nlxb3R7/fX4/XxwcHRxbGpqaWNka21tcXRvcnVxc3VsaGdmZmdnZGdpam11fP78+fz+9/f18vt9e3Z2eXx5dn759/Xv6enq6+zw9vLw9Pf7/Px+eXV1d3NzdHBvdnx7eHb/+v3++fj07ezq5+Lh4+nr7O3s7+7w+Xl3ff53bm1sampud3h2fPv8+fT1+n7+eHFydXl0c3RyeP98dXBxdXV1e/98fPfu6+Xi4eDg4+fm5+vx/Xp5e3v7+f10bmtpaGZjX1xdXV5fYWFeXV5hZmdkYGNrdHb98u3m4+Hf3t3e4urv9vrz9Hhub3B3/Pry7e3s6uvr5ePn6Ovv7/Dv7/Lz8/b58vP59fLs7Onn6Ofp6+np6/R5c3JtamVgX2FiYV9gZWZlZmppaWZkZGVkZGNnamxpZ2hucW9xcHNxfHj3buf9bfzl2u3kbXpw4OTe53tjWl9eamNrZHrv3+De3uXp/PH57/b2enBudHv78vf0+O7o3trc3N3i5+bq7vr9ff/59Orr8Xxzbm1vdHl0cWhnaWptbWtoamxta2xubmxobm1uc3Z7dnF0eHRsampqaWtsbm9ydnVxbHL7e3h3ffr07/f09O/p5eTj4uXo6+/w9fp+c3J1fvfz8e/w9fn59/f8ffjw7u7s6urp6Onp5ujo7PP48/X4+vt+fHl0b2xtbmxjZGZiYGNlZGhqa3F5cXFvbWtra2xtbnF0dHzv7erm5ent7e7v9fj7eG52/fH1/Pj2+fr4+f39/n16/PX3/Pt9ffbv9/rw9vv+/nt4dnR1b29vePz4+Xx8fHp4d3RubGppa2ptd37/9Ozp49/h6Ozs6ujp7O3s7/h+fnxwcHZ6dHF1/Pb7+v51cnZ5ffx8c3d9fPf08/L39u3r7PL9cGxybmloa2hpbWpnaWljYmZrZGVnbW5qZ2Vqb3d2c3NtbXv+fvjv7vDu7Ofg4ufm5Ojp4ePi4N3d4+jg5Ovo4un2/vr++/ZxaGhranT9fnx7d3V+/nhxcHFvc3t9e3x+en76/nt5enZ5+PX9+/j39/v4+fx6b21rbG5wcXN8/vz39vZ+eHZwcHJyb3F1dXl8dm9wdXZucHz5+fv4+Pvw7vl0a2hmYWFkZWZnaGhrbXN7+PHw7uro5eLk5uTm6Onr6+zt7Ovt7+3w+f99fH3+/f/+fn348/Tw8Pb4+vHp7O7w8/T6fXZzcW1qa211cW9tbGtrbm90dXx+9/Xx7/Hy8PV8e37+e3n/+3p8/fz9fX77+Pf7+nz+9/Dw+Pj4/Hlwbmxsbm1tbGtwd/bu7u/t6+jl5+vv+nl5enJva2ZlaWxpam1ucnFvaGdoaGpoanBsbG5zd3lwbG1z//r18e/v7efi39/g4N/g4OLj6Ovq6/T6/nx5dHR3/PT6fHp7eHv+fH739vn59vTw7/T9fnl5fXduZ2Rlam1tb29wd3n+9fLx+fp+fHx2c3RwamRiYWRnam5vcXZ1ePz09/vz7+zn5Obo5uns7/r7e3JxdH758/P6+Pf17+/t7Ozu7urs7u7x9n5vbG51c3Bxb25ucnv79/Xx7+rp7fX3+3Z0dHBrZmZkYWJhZmlqb3V1d/9+//n4/nl6dXn+/3h7e3d+9u/t6OXi3t7e3t7e4erw93x2cWtnam51bGxsaGZnbXBua2lsc3h9+O/w8PH19/Tw7u3r7/Lt6ejp6+7t7fH1+nx6eXdycG1paWlpbG9xcHdzb29ubGxubm5ydHr7+fj5fHRxeH7+/HZwaWZmZ2dnaWhoa3F6/vTz+P398e3s7vHw8O/u7Ozw8O/w7Ono6O3u7ezs6ufl5ubk5enp7PT3/Hx++e7x+fb0+Pz18fT9fntxbGxpZGNjYmJiZWZmZWZnbXV3c3V1b21xdnp6cnFwcnV7+fTz9vfy7url4N/j4uTk5efs8fp7fHl2enhzeXpwcHV6/vX0+Pr7+fb6+vr/e3h2bmllZmhoaGloZmVkZGZpamxubW94+/D0+Pz9/vz2+vX4e3h7+/nz9vr5+vfw7+7u7/H09fPu7vL18u/t6+np6erq6+vp5+Tk5+fk5Obr8Pd+bWdjX1xdXVtaW15dXV5fYGJjaW9ubW90evnz8O/t7O3u8fn9/XtxbnZ9+vLy+fz38fDy8Ozr6uvs7Oro6/Dy7uno5eDh397d3t7f4+Tn6OzzfXFsaWlnZmZjYWFkaGpucHByefv28e3t8/p+dW5vbmpmaGtpZ2ZmaWxvdHp7fP307uvv/Hpzb3F3fHp6fvz7+PDz/f719Pv7fXZ6fXJsa2pqaGlqam52dGxqa29ydXd8+fTv6+nm5eTk5unp5urw9vr+/vn9/np1eP/8/fjx7u3p5+nr5+br7u70/31+fHVvb3B0evv7fXp6/fn7/P57d3V6eXp8dG5pZ2lrbGtnaWloa294enn88/Hv7Ozz8+vt9vn2+H13dW9sbnv49/b68Oji4ePo8fx+/X50b2tpbXf++PHr6uzo5eTo6+7y7+3u8fV9cm5qaGVmZ2VobHN3efx7d25ramhpaWtsbXB1fXx++/x+/PL0+Pj4fX59eHFyfvn3/H1+//7+/v7+/v7+/35+fn5+fn19fHx8fX19fX5+/////35+fv///wA="
                        };
                        await SendToWebSocketAsync(openAiWebSocket, first);
                        
                        Log.Information("Sending second payload");
                        var second = new
                        {
                            type = "input_audio_buffer.append",
                            audio = "UklGRgBTAABXQVZFZm10IBIAAAAHAAEAQB8AAEAfAAABAAgAAABmYWN0BAAAAKtSAABMSVNUGgAAAElORk9JU0ZUDQAAAExhdmY2MS4xLjEwMAAAZGF0YatSAAD/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////fv////////////9+//////9+/////////37//35+//////9+/////35+fn5+fn5+/37/fv////////////////////9+fn5+fv//////fn5+fn5+fn5+fn5+fn5+fn5+fn19//7+//79/f7+/fv8/Pz7+/v7+/v8/f39/v78+/v7+vj39/f39/f4+fv8+/z/fHp4eXp5eHd4eXl6eXp8fX3//v79/f38/f39/v79/fz/enZzcXBwcHF0dnh5eXt9fn17eXl2d3l5enp5eXt8fX3+/v///Pv7/Pz8+fj5+vr5+v3+/359ff/9/Pz7+fx9fHx7eXz/fX3++vb18fDu7e3t7e3u7e7w8/X2+v19fHt7eXV0eHt7e3x6d3d3dXRzc3NydHh9ff339fT19fP0+Pr7/v39+/n8fn17dXJubGtrbW5xdnp9/Pn29fX6fXx+fHl6eXp4dnn//Pv7+ff4/H5+fXp3dHVvbW1vc3NzdXl3d3l5eXl7fHx7e3p5fvXu7e3s7u/w7u7v7+3q6enp6urt8vT5+nl2c25raWtscHl9/Pfy8/Lz8/P5/f38+/r6+/r39Pf//v57d3V4e/78fHNta2lnZWdqamprbnBwb25vdHp4e/74+Pv6+PHt7uzr6efq6err7e/w7uzs7O7w9Pv/fHp6eHZyc3V3e3l+/fv5+vn28/Dw8/X4+vn+fXl3eHRzbWtrcnh0cW9vcnFubG1tbGlnZmRkZmdmZGJmaWpucG9udPn08O7r7Onm5uXm5OXr7e7t8fT6//57eHl5eXz47/D29Pf39/b07vV++fPs6uzp5uTi4+fq6+7t7e7s7/j+/nhwbWppamdiZGZqZWJlamtrbn7+eHl6fP38/vz/9+7u7uvm4+Hh5uju9Px8eXBranBwa2xtcXFsbHB3eXBt//j7e3t7+vb5e335+3l3enhzbm9rY19fYGBiaG55fHv9+Pd+fH1zbW5wcnpyc3h2dnNyfP/+/fLr6efl5+Pf3d/e3eHk5+fj5OXg3+Xp7vP5ent1eHt3enVscP5+fvP56+Dg4+rv/PL1cmhjYGFfX2Vrb2tqbnZ8dW1ta2tiZnX+8eri4en29vr37vB1bHd5dvrz7fL27+zt7u5+fH51a21ycHB9/PX1+fb4/H3y8fP6e3VvZmBmaWhmZGlqbGpsbmxsa3nw7O7l4uTo9P1zbGtmYGNpZmNkZWhrfHr38/ry8v51eHx9fvbr5eDc19fV1tfZ3eTq6+z19u7w8PHz9Ph6cWx2fXR0eG9ua2ttbW5mY2ViZWtxd3p+8/D7ffju8/737+vyfHRxcW9ucPH1fPXs7vr+d3xzamZka2ZiZXJ993hre/t9fPr6+vl1/H51bmptcH55//l9fPvu7+75+urt8/z4+Pz+fPTu7+zp7O7t6+3zfnp7eHVwa2lrbGZzeHvu7Ofn7e/y8vT29/X193ZubWhhX2NobW17fvn/efby7u7q5ejp6ejm5+76fvn+df79cWlqb21tcHRxb2lnaGVgYmlucGtv/H599Ovx9Onk4uTh4+bp6OXi5ers7/X8+n77fnFubnNye3tzdH36/P//fHJpZGlpaW9+9PR9eH377fDs6fF8+u11ZHL39X776u9+9+fq+XJ1+3tqaG1wX1tvb2JiZmZnZ2BkcHF8a2VodvPq5+zl5Ovt8/LsdWhjbufd5fj99vH19vj6eHb86+zo7XdoZHj3d25ofO95ffDp/fXp5+bi3OHh6fHi2d3s9/vv7P9qbW5ydX7vcW3x9fxoZ3Jvb2p1bWJr+uX2a2t6amd4/P1uaXr9bGtyb21nY2Rwbl9fZGpqZ37vfGZo+uDk+Pfs5+vt5+1uaHvteWVq6uP+b/Dn93zr5e72+H76/fDs9m91eXv4/Hv68Ph+ffX6bGJiaGhtbmBk+ODf5fDw5N/l5Obs8O7x9PTvfmxx/vf1b2T84+fz9+vj3+zt6evv9P9xdPjyd25ucv3y+HVuenVwePt2ZVxdXl1dXmFiZWpoYl9iZFtYWl1nefx5efnr6OLf4ODu//bs7O3y6+jr9vrl3N/p7Ojj4OTk4+Xo8PDs9H51eHRwa2lrZGZv9vH17e3j3+fr6ejwdG52em9rbXpyZmRpaG1zcGdhYmRiYWdnbHh5/+7g4ejs4dze4uvv7X1tanJ7bmRdXWJlZGtsZmx6+f7v8/3z+v52dG1hXWJw/vby7erm7Ozp5+TrfHH9//1uaG9vbWVucXt5bHd++evj6u/r5OLl6OPi3uDj4ODg4+31eXB0amBlb2tlY2puamhkcPDx+e/p7PDx8fX7/fx7bmxrbm1lZ3B2dvr58/HvfGpmbH5+cf7/9fXw6/Z2/v37/fV8+fHs7ero5uXt8nx3bmRjaWdhaXBvevnt6O3z5ubp7/Tu7PF5+On0/fb9e/r1+ndza2lsbmdiZWlsbnBqamx3dHVwbnF69Ph4bnR5fX53efnu6OTl5+fn6Onn4N7f5Ofv8fV5d3v7fXp+fnp4cm9rbG1vdXr8d3N1d3hxcG1vb21lZWJeX2VubXB59vr6eXZ8ef19dnz99/5zc3vx7e3s6N/f3+Hg4eXl6PB9cnB1e3BrZ2lnaGxzenX08/j67e71/Prq6ujm5OTg5ezv8O/z9Pn5+3dpbW1vaWtpaW15cGxnaG1qa252bmpobG5tbm9ye354fX17bmpobm5udv3/e/js6uvk4+7++O/u+H17bW1zenN+7/Lv/P7++X5ydH1wbHR+fXt8fPf8+vDq6fD38O/+ff38/X3x6u58fPh4d3x9/ffw8f1+8evt8O3r8fPu6O31+3p3/vrzfXl6fnxzb2Znamhpcnp8eHvw9/398uvzb253dW9rZ2dkZ2txeH319/Lu6uru6+759fH5fHp0eXNvb3Jta2lrbWxtbGhmaGlqb3z2/v318/T57OXi5eTi3+Hi5efs8vf9ffvt7O/1+PHw+HNpZWloZ2t6enp0bmxrcnZybGtscHd3e3l5e///eXl2eHj78u3s7uzv9Pr3+ft7ffPt6Ovy8/55cGtpZ2hrbm5y9+309+7t6+rn6OXh39/k6e309Ph9dGpoZ2NnaWpsdnx8d3d6//p1evv5+XZwdn58e3X5+f58dXZ1dWpqcG5vcnl+enJwdnVvdHv5+3347/X08fL18Ozi5+rr6Ono7Ovr7Oz1eXJ9eXJzc21sb29wb25ramZqa2twcG96//j3/fPz8fP48fp+e3d9e/Lu7ezt7Orm5u3v8vDw8fXu6unu+v37/HdyfPb9fffu7vLx9/j8+/f6/Hxwbm5sa2psbGtpa21qZ2pubWpta3BubW5wcm9tampqaGhqcHBudH307evp6ezt6+fr8/n19vv27urr7O3v7vT1/nVwbmxmY2FjYWBjaWx0+e/q6u3x9vx7fP99enRwcHj98/P9env69/Pt6err7vDu7u7v6+707u3w8/T2+f/9/3767uzl393a293g4uPm6fH6fXVydPzs5+rt+3dwamNeXl9hY15cX2JeXVpaW1lWVlZYXWdwe/Dl5ubk5enr6u7u7evr6ePe29vg7HplYGn02M3IyMzccFJJREJER0hJTVZiam54eXJu++re2NXS0NHQ0tTW08/NztTnZFNQWfjPwry9w9RfSD45ODg5OTo9Q01i5dPO0Nbd4d/Z0MzIx8jKysrJycrLzc7Q09TW1dbY2Nri+mVWUE9VZ+XRzM3Za0o9NzIxMTM2Oj5GT17+5NnW19jc3Nvb2dTPy8fFwcLDx8jMysnJzNfb29jZ2/BYSD8+RVjUv7m5vs9YPTQvLS0uMDU5PkZX8NLKxsfM0t3b0sjAvLm3t7i5vMDH0N/9amRocnD6+WtfWlVSTUtOV27m6GpOPzk3OUV9xrq2ucPxRzkyLy4vMTM3O0RX38i+u7u+xcrQz8zDvLeyr6+wtLvE0+54euvc1dLT5PvqfflgV1tgduvqY0s7NDAyPFXNvbm8ymE/NC4tLi8xMjU3PENR5MzCwcXK0NDNyMO+vLq3tbKxsbW5wM3hem9seO7m4e1tZV1fXlhWWmJycV5QQzkzMTY/Ysm6trnC5kk5Mi8uLy8xNDU6RFzVxr27ury/xsnHwLm0sK6usLa9yNHf5O3v7/Ts7un+//74d19PSUdLU15kWEU5Mi8wOU/MubO0u9NOOTAuLS4vMTQ3O0JR5sq/vLq7vsLHycW+urSwrq6wtrzEztjc3dzd4uv0a11ZWWFdVkxJSU5YX11NPzYvLjI9ZcO1sLO+5kM1LiwsLS8xNTk+SnjMv7q5ur3DxsS+urazs7S2uby9wMXJzc7Ozs7P1djh7fTwfWRQS0lKUFtiWEU4LywtNETdvLS0vNZINS0rKywuMDQ3O0BN9cu9uLe3u77Fx8K9uba0srK0t7q/yNPj+Hv87eXh4fZ0bnZkVk1LTFRjbmFOPzUvLjI/9b+0sbS/6EI1Li0tLjAxMzY8R2nLvbe2uLzBxsW/u7aysLCytbi9xM7Z5+3r5OHn+P1uX2Fn+G5ZSkNDSFNibVVANS0sLjlcwbOusbzhQzUvLi8wMjIzNjtI98i8t7a4vcXMzcrFvrm0sK+vsba8x9bl8/j89vlwd2VfanzzalZJREVNZPB6TjsvKyswQ9O4r6+3zFQ8NDAwLy4tLS01PlfSwbq4ury+w8TDwby3sa2srK6yu8PQ2trb1dXh/2BfZ2Js9OR5WEU9Pkdf4eBbPS8qKi9C0rq0tsDgTj87NzMvKygpKzFEfMrBwMC9vLu8wsnOy76zraqqrra/zM3MzMzY9GNbXuvZ5uv3dF9NRENKW+jeaUY1LSwvO2vCuLe6wtF5UkI3LiooKCwzQl7ZyMC9u7u8wMXIw723s7KytLe6vL3Ax9Tn7eTc29rc2fdscPhfTT46P1Xl124/MCsqLjtczsLCwr6+xeFCMCknJyotMDQ5RfLGvLq+xMLBwL69vLixrqqpq6+1vcLDyNR5WVZl39jsaWRdUUtITmrf5GNFNi8uMztKX+XLwLy9yW0/NC4rKScnKCw1R+XHvr69vr2+vr28urezsK+vr6+xtLrAyc7W3+ry3drr6tnbW0lCSWvebEU5NDQ2NTQ3QmXMwsLL3V5IPTEsJyYmKC0wOknjyby5uLazsrS1t7azsbK1tre2trnCzNPSzs3dZ+fOz2xLRlrd4VtFQ0RDOjQ0O0ti8+POxMDMaEQ7Ny0pJiUnLDA5TerDuLOyr6+wsbO1t7a4ubq7ubW6vr6/xcXNZ/jbdExOTFjy92d8bk1OQzg1ODtBTUtc2s/qYVRFPDEsKiclKCoqMkFU0r65saysrK2vsLO6vr7Fxr7Dxr/CxcDG3crM7lpmT0tRTU9YWlBcTklKRT9BREJMVVZaW1RRSDwyMy8qKy0sLjo/S+nHu7Ktqqenp6eqrK60uL3Jy87m3tXY7c3Dyc/P0v19WE5HRj89OTY5OTk5PENMVWhybGlnS0Q9NzMvLS0uLjU6QFbcy7y1sKyrqainqaqrra+2vcTO4nVaUEpeVVFW6nNrbGxvYV1QST4+PDk3OT0+QklXau3iemJOQj02MC4uLC0vNTpEadLAt6+tqqipqamrrrC1vsjP/ltQVklPW1BPafJu8OvX2unp51hMSkE+PD1AQ0pe+vrg2eteVEU9OTQwLy8wMzY7RmPaxrqyrqupqKipqq6xt7/L1XdcVVtNad1+ftfV4dTe3PNmXFJEPj04ODg7PkZOZuzp3eNuTUU6MTAsKCkrKi0zOkVh18a7tK6sqqioqKiqq662u73N5HNvSll2VVJnbGvf3dDU2+ByS0JBOjc1Nzo8Qk9katrW62NZRDs6MC0uLiwxNTtO5sq7sa2opqWkpaaprrO3wtXbalVaXFFZ2e1z6tnz725gX1BKRUA5Ozk2Nzs9QkxafOrr9WJSRj82LzQuKi0zMDhHU+jCubStqqelpaWlp6uusrrM3PNWRkhIPEphTEfzbV3t+WpuWUNCOTQ0MzM4P0dk287HxcjO7ks+NS0sKSYnKywxPU7cvbKuqaalpKSnq621u8HL1/xobG9m1+Xpx8f0ctFyX2NfV11MQUdAOzs+PUNTXOXNysnGz2lPPzYvKigpKCYsNDpN0b6zq6qno6Wnpqius7jF0dLubOvh78/M48a8zVvpeVVXTkVISDw+Pjc5QUA+SVzt3Nre33pIOzMtKSclIyQnLDVAVs+5r6ypp6amp6qtsbe7w83Pzc/S0M3KvsfWx8LkTE1ISktHQklJREM7NjtEQUFNY9nO1uLd/Us9My0qKCYnKiw0QnHFt6+sqKamp6irra+1ur2+wcHCw7+/v8TE1f3a1VM8PT5FSUpGSlRWSjo2O0A/PUJLetjbe2hhSz0yKygnJiUmKjE/X9G+tKyop6eoqaqts7y/w8bJzczHv7/BxcHJ1M/TXEA/QERKTUZDSUtFPDc4PEJITVPvzsrO2nVOQzguKSgoKCksNUjgwrmvq6elpqiqrK6zvMLGx8nLzc/NysrLzODo09VWREJHVGZbS0tPT0Q7OTs/RU1UZ9nKytDpVEQ7MSsnJiUmKSw1RuLBt7Csqqioqautr7S7v8THycfGx8jIycjJ3eDZ409EQkZSXVtNTlVXSD07PD9FTVRq1svIzNxlTj40LSknJicoKzE9Wc++tq6rqaipqqyus7m/xcrP1dfS0M3Mx8nSzcrTW01LTVRUTUZGR0hAOzo+REhKS1Z25/djWExBOTAsKSoqKiwyPVDYwLevq6inp6enp6qtsLW5vcHHy87R1NXX7djR3F9VT1ZqX1VNTEhEPDY1OTw8PkVW69/i7nhbSzsxLCkoJiYnLDVAac27sKupp6anqKqts7i9xMzV3t3W2dXO3dfN0lxQTkxOS0dDR0VEQTw8QUhITVRr29zq+G5PRDkyLy0sKywtMztGXdi/tq+tq6qpqayusrW7wMTIys7S0MjOxcHG0tfe6d1vYV9XS0xGPT0/RUdMWOnMztDY5FdCOS4qKCckJSktNkFX1ruzrqurrK2utLm/yszN2ODTzL/DxLq4vMnRcvRfTUtGREBEPDo+S3Lfz8i9v8ncdE89NCsoJyQiJSotOkrwwrezrqyvsLC1u7/JysPIw7y6t7K4tLC2wMvrX21OSExLRElHQEdNXO3X1cjAydH2Tj0zKiYlIiEjJyoxPVTOvrewra6ur7O4vcXHx8nFvry1tLivrrO+xtzW3FpXWUpAQTk6PkZh3tDLvb3E2FpANy4kICAgHyEmLDlDcsC3tq+vr6+ztre7wby5u7u6s7G6tbG1wsz029N3cHteS0k6OTs8R2fb0cC/vtFpRzgqJCMfHiAkKC41R+DHvbe0tbS3uLm7vbm1tLKzr6yysa2uusbZ28va9/J1Tks8ODo9SuzMxru7v9dVOS8oIB8gHh4kLDdCVNq+vL27vL27uru4ubawr7Cxrq2vra6wuL7MxMDFyMjiVkU3NTY5R93Jvbm5v9xHMykjIB4dHSEmLTE8VN7PxsO/vsTEw8C/ubWwr7CwrbGurK+4vMrDu7/Ewcp5UTs2NTc+X93Fvr3E20k1KyMhHx0dISctMjlO3tXMxsC8vL67ubm0rqurraysrqyssLe7w76+w8C/02lMPTk4O0vnz8C8vcnoQTApJSAeHiAlKCsvPEpcbtfNxsjIxL++ubSysK6urK+vrKyzuL67uLy+ur/XaktAPz1Dc9LFvr/H2Ug1LCYiHh0eICMnKzA/TV7aysTCycnFxL+6tbKxsq2trayssLS5vb29wL7C0npRQUBCRVndy8DAx89gPzAqJiIfHx8iJSkuPE/sz8G8ur2+v76+vby5t7WxsLCtq62vsbS0try9vsndZUxLSklU7tnNysvQ7Eo7MSsmIyAgIiQmKi85Q1fmy8G9vbu7vb6+vr27ube0s7OysbO2uLq+x9Pe3u5iVU5OT1BNTlBVU0xFQT45NDAuLS0tLi80OT1DTF7r18/JwLy5trOxsLGysrO0tbi5u72/w8fLz9fc5v1pWlRTT0tIRUNBPz49PDw8PDw9PT5AQ0ZKTlFTVltdX2n139nV0MzJx8fIx8bDwMDAv7/AwcPExsnMztTZ3eDh5+339X1qX1xYVVBQU1NUVlhWVllbWFRSUFBRVltdYWFhY2Voa25qX1xeXl1dYWNnbHf48fx4ev318PDw8f55+Pf6enzz/Hl8+/L07Off4N/i5OTm5Obq6fH67+vs6+Db2tfV1trf5OXr7vxuZWBgZ2diX2RteXV4dnVya21ydG1qbm1rbG5sa3f4+/bv497d2tnV2NnZ2Nre5Onp7Orr7fDy/HJ2fnlnXVtZWVhYWl1gY2FoZGFdWVdaWVZYXWZv/fbw5eHe3OHm+H7593746eji4N/d3N3e4eHg5efl6efq8Pj+d2xlc3RnbWpfYmRdXGBQVFNbZlJM8vBy2fVTXvz32NHZ2eXt1O7rztn+3drs2uHv/W7+2tjPyNDGw2lh2mtUXEtWWUtR6PN73t/X2HX38GBQS0VHSEJLdF5W5MvT1dzL3GZu5ltVZvDc1dLFxM/Pyc7O1eHr6GBr+mJcXlZda1NefGZhb2JeW1VjblpWbGdYZ/H7fmvezN1w3e5raVlu8FFX8P5jXV7dYU185Vlpbl1vV0lyb01aflZhal3k3WL40d7x4NPW7/vd3+n+8e93YnVvX2j57t/f1svR3dDM39fV6ebX5+7g3tTPz9fc2t9ra/R0a3heYm9vdfDq6Plu8m5bY2JdXFdSWW13Zm3m4Gxu6t1qaO96Wu93Z+bmft708tVzWfxeSmBaTltsXGhhWepfUW1vT2xxTmLlWFftaGp5fmXq3nRq6/xlcfh0b+ne6Pzn5eDV2t3Q1tjS3t7V4fbf+Pzid2P5d1tw7WFm5HFe5nJe4OBe6Nhq59Hmdc/b5eXk6u59fOVq+O9rZe9jbOxYWXBmU3B0W2psZG19X3JuWmhtXV19XV5nX2NkYm58WW3obl5tcm/penXd2XPt0tn33dTg3dfn28nV6c/J5+3a63VqZmz0Ymjv/Wp9aV36Z1Nbc1hNU2FpXl5nZ1xfXmBwbGJqenVvbfLd6G3n1+j63dzj293v4dr4/d/obHzocGZy/PPsfmzu7nBneH5rXl5nYlxaXm56ZF7+6nZ84+Ll3+nt3eV77N/u6Nvc4NjX6urd3d/g5ubm9HJoYXHwbGbs4G5heXJsaF9dfHZbYfxtWVxmefT8eePY5fLn4u77b//s/WZt9nxuZ2huZVhccWRaX2tmbX1tdenya238/HRxZWd+c19gcHJoYWpscmpgYGJjXl9iZGhubHjo5unj3t7d3Nza2tvf3dze397n697j8Ozf3d/e3dvc5OHb3uPr9f39c2pnZ2x+cmx+/W5mY2RjYV1aW1pZWltZWmJmX2Bq/uvq5N/k4uTj6fT++v18/Ovf3t3Z0M/NzcrIxczdzMLH7Pfq2NDcYExKRkI7ODtFXu3d18zL1HdNPTUuKigmJiktMTlL2761r66tra6wtLm7u7u7urm3tbW1t7m9yNbwaE1FQkdHSElNXfXheFVFPjk0MC80O0lf38zDvr/I4VI9My4rKSkrLzhH9si6sKyqqqusra+xtLa4uLi5ubm6vL/FzNvuYE5ISUVDSFJTS0NCSllnUkE5NTMxLy8yOklu1crGxcbN7Eo4LyspKCkrLTRAX828tbCurq6vsLS2tre4t7e3t7i5u73Bx83gaVhRTVBOS05hZlRKREVMVk9FPTg1Njc2OD1L/8zCv8DFzNhuRzkxLSwsLTA4RWnMvLOtq6qrra+zt7m7vLu6ube3ubq9v8fR6F1MR0RDR0xMTF7wd1RHQEFKTUY8NjMzNjg7PkZY3sjBwsjU7VtKPTYwLi0uMzg+SnfMvLSwr66usLG0uLy9vbu6u7u7vL/Fzt9lT0dAPj0+QUhWbX7i2OJcTEZDRkhDPDk2NzpAR01a99DEv7/EzOBrUkU6My8uMDU6P0/jw7mzr62srK2vs7m+w8XEwL69urm4uLvBy99nTkU9Ozw9QUlOUGHwe1ZLQj5AQ0A9Ojc4Oz9GTlt22crEw8nYc1ZJPzk0MC8xNjtDVPHNv7m0sa+wsbO1uby/wMC+vby6urq7vcPM3W5US0ZCQEBESU5UVl5qYVBJQ0BESEhCPTo5Oj1BRUpPZ+HPysrO3W5USD85NDExNDk/SmnSwLm0srCvr7Cytru+wsPDwb+9vb29vb/Hz99yWk9KSEZGSExVWFZdc3deUkpGSU1LRj46ODk8PkRLVnDazMjJz+FiT0Y9NzMyMzc8R1viyr63tLKysrKytbi9xMnLx8TAwL++vr7Axs7haVhOSEE9PDw9P0RFS1RfXFlRTE9UTkdBPTo6PT9GUmPn0MjExMvY/ldHPTg1NDU4PUZX5c7Bu7a0s7S3t7a2uLu+wMC+vb29vby9v8bP42ZWTUlCPjw+QEVNTVNg+G9gWE9SV1FIQj8+P0RFSVJq3s/Ly83W6mhVST86NzY3Oz9HUXbVx7+8uLe3t7e4uLq+xsvLzcvKyMjExcfK0+ZtWktEQT8+P0BDTE5OW3RgVVVPTk9OR0E/PT4/P0BLW/rVysXFxsnQ5F9JPjk2NTY4PEJQ6c/Bura1tLOys7W3u77CxMXGx8XBwcLFy9TkblZLREA+Pj9BRU1TYubc5nv+eG5mWk1HRENFR0pPYuHPx8LCwsTN4l5IPTg1MjEzNTlATlzVwb6+uba2tbi8v8PLzs3Oz8rDw8XFyc/V6l1STERAQEFBRklNWuzl+u/oe2VeV0xFQT8/P0BDS1zz2s/Nzc/Y71xORT47Ojo7PUJMXfHRxr+8ube2tbW2ubq9vr/Cw8PGxcfMzczY3fN4X09PTE5FRUdNT0tNTlZOTEdMS0lKTFJPTk1VWlVSWGBkaGx2dmVeXm9fWU9TWVtaYe7k1c3Ewb28ubi5ury9v8LHzM7W19ng+W57b2tfZF1XUE5MSkdFQ0RFRUVFRUZIS01PUFFUWlpZXV1eYmZy9ezr6N/g3Nra29va1dTV19XT1NXSz83NzMzLysvMzs7O0dfd3uvu+HFpX1xbWlhUUVBOTk1NTU5OTExMTU9RVVpeX2JpbnJyfvr49fns5eTf3tvY1djW1tbY2dna2tnc2tzd5N/i6Ojj4OXm39/m6ez0d21jYV5dX15bWFdWVlpbW19pevPy7uzv831tbGleWVlaWltbXGltfO3i29bX2dri4u3tdGtmY1tYWVxfYWBkZmpocG9kZF1fWFtcXmdndPnn5NrT1dbVz8/U2tzc3+rs6+l2du7j6ufk2tva19bU1d7c4t3s+/X5emp0fmhtZmhhXF5eYWhjY2dvZ2Z6amxfYFBbXFpWVGBgcGLg5+b269zh7WTb7fZpeO5qfl/q/nBfeXty6HTd395t3+7pdelq82dfY/FYeWtkWnFnYnVa+Hx0XO9wXvZocHb7Wvf+eWz8eOntZ+Dh6G/n8dPs8u7S3eLe8Nv7dXHh9ePwe/Lp7vLb8tTs6d3Q7PB37PV7VvT0XWH7bmxeZV5jV/155WTU/NLne2b5Wl5fb+rgeODRam3ZXfL7WW/ebOTb7uTH/OLj/trrVPLcW1H5aN57V1XTTUhbV15OTVbdT092e17lbebkbe/WYf3uZ1noZuje+/vpX+rfe+rQX1lsVEhiVfPb5nHPz/nZ5+hyc1PpdWn05/Do2V3V3O5t7v/ealzk3mP609vq3e7T5Od842f2WHJxeU1ebFbf9PB9y11uW/BoZlD26P9s+PXPXm3v0ldwcN31amn/4Hr3fsztZlbt+/RQ28/XW9Dl1GRk+tFaWvzoe11U3tlmXXno62pX6m5yWmXs3FRdd2ldW0/f60tc2N/r4mvR8U9s0Xlqb3TWe1D70FxmV2dh9Ub7fe5U4O7Udmbv3WBX731aWGdrdHDx4tfn83zs6eN+6t/d69/i2Nbd3tjT62Ja8ON0ZfHa5nHw3eNiWnF7YVl66PtaYPV8VlJ95HJg+9vcZl/t3G5v1sfM09zQzNl2493ubFZk9lpLZmNZX2FYdWVMWGxrafJ86fRwaV1eYVVUb1pYX23+8GRl7/FxXmT4fFRa4t7h3dXM0/nt6ntq8fbf7Wjw3Ppi6dr3UlRt9FpPc9HfYGXi219Vat7uX3DY3Gdi3993bOnQ3l112eHxaPfM3lJwz31f+OnqfVtp3G5RXeljWmbk2tjk3dPU6efP2+Da2NjdaezW/Vhpb2VZTlt9allhaHRhXmr273Nqc/xaWW9lVV1gXHR0WWtrXF9wXnvsaGjt/ltx6fBcbe/sbm72+3pubWvp9Prm1eDm2tLZ18rP3dve/fF2fN/fbnrx4OJpaOHoW19xbmFiam5fW2ttYnftaHvfcm/pfVp98WRm7t/j4enq29t89+Z5afTn4vV3eettVmRjWVBPTlldXmv06/l59P1pY211bnZ2+3ljWl9kU1VyeFNl29Xe2c7Nyc/VwrbF/cu8xM3IxMZqQkVIPT9k3tbNzM/VVkA/Oi4tLi0sLTAzNTc+TVRY3Mi+ubOvrq2trbCysLK2tbW9u7zAycvZ7N/f4uX1TkM5Njc6Pktf/uDoZFNIOjcxLC4sKSkuLS4zOz9IWvHKw7u0rq6sq6ytra2vsrGvs7KyurW3wNLJelzp6fnsYkVDNzM2ODg/R0ZQUUxIQTgzLiorKicnLSwtNT0+TGfdw7y1r6ysqqurrK6usLS3tri1vr63vM3MzV/u2tzf111ORj06Oz0+SElNVVVOSD84NC4qKygkKCsoLDc3O1JZ/cG6t66qq6mpqaurrK2wsrKztLm3ubrFx9Xn49rf7+tUS0A8OT09P0JKS1FPT0pCPDgzLSspKikrLC8xOj5JXtrIvLezr62srKytra6vsbCztLK5urm8x8jZYG74Y2J2TEVBPDg7PD0/R0hPVlVOST87NTAtKysqKiwuMDk9RFfdz762sq+rq6urrK6vsbS3ubu7u76/wsXIznxwc2thcFdMSEM7PDw8PkVIS1ZbXFhQRj87NjIvLi4vLzE2Oj9ObdfEvLizr66ura2ur7Gztbi6urq7vb3Aw8bR8PdwZmhiUEtGPjw7Ozo7PD5DSUxNTElFPzs4NTMvLjEzMzg9QExz3s6+ureyr6+urq+xsrS2uLq7u7y9wMLCx9x1ZFtZXlRMSkVAQD8+Pj4/QkdLT09MSUU+PTs1MDIyMDM3NzxHT2jWyMC6trSxsK+vsLGys7W4uru8vLy/v77CztXc5+jnaVdSSkRCPz0+Pj5CRklOT01KR0A+OjY3NjEvMjU4PD9DUHrayL25trOxsK+wsbK1uLq8vr+/w8nIydb/XlldYVpXWlJJSUpEQUFCQkNGS1BOTk5FPjw5NTQzMDE1Nzk+Qkpd4c3BvLi0sa+urq+vr7CxtLW2t7i6vLy9x9La8mZvX1BNSkRCPzw8PDw9QERJTUxMTEg/PDo2MC8wMTM2NjlBSlfnzsS7tbKvrq6trq+wsre6vMDEwsrNyMvZ5nhq9N9+bmtYTktFP0BAP0JGSEtNTlFORkE+Ozk4NDI0NDU4Oj5MYODIvbiyr66tra2ur7K1uLq9wMLDxsbFytbg/mt1eGBbWFBLSEA9PDs6Oz5DSUtKSEZAPDo2MTExMDE1Njc8Qkps0sS5sq6trK2ur7K2uLq8v8HExcjNy8nT6+71b3V8YFhTSkdGPzw8PD0/QURNUVBSTkdGQDk2NTIxNDY5PURS787CurWxr66ura6vr7GztLe6vLy8vsC/wcfO2eXm5n1jWU5HQD07ODY2Nzk7PkJDREVBOjc3My8vMDAzODg7RlBi2MjCvLa1s6+ur6+xs7S2ubq8vr/DycnJ1uXmdmd3YlZTTUVAQUA+PT1AR0pKS0xNS0ZCPjw4NjY2ODo8P0hSY97Mxby2s7Gvr7CwsbO0tba3uLm5uby/v8TQ3e1nXl5TSUZBPTw7Ozs8Ozo8Pj9BQ0JAPjs5NzU0MzQ3OTw/R1Br18m/u7i1s7GwsbKztLS1t7e3uLq7v8PHz+H2allVVVBPTktGRUM/Pz8+P0FBQ0ZHRUI/PTs6ODY0NTY3O0BETGzcy7+6t7Gvr6+urq6vsLGytLi6u77Cyc/d8WZWUVZZWVtVTElFPz08Ozo6Oz5CRkdISUZCPjo4NjUzNDc5PUVOZtvLwbu2srGwr6+wsrS2t7q8vr+/wMXJys/V3u12al5ST01JQ0A/PT08PD09Pj9CRENDQ0A+Ozk4ODg5OjxARk9u3MzAvLe0sbCvr6+vsLO2uLu8vcHFxsnO0Nbd4vlkXFtTTkxJRUNAPT08Ozs8PT0+QEFCQUBAQD89PT4+P0RJTlpw3M3Dvrq2tLKxsbKztLe5u7y+wMLFyc3R2+X4cGxfV1BOSkdFQkNDQD4+Pj4/P0BBQkNFSEhKTVNVW11ganvz8Ojp6NDOzsrFwb6+vb27vL29vL7Aw8bIyMzR1Nro+3xkWlBQUlJLSUhIQ0A+QD8+Pz9DQENARD9CQ0ZHSFBRZmf9/NXX1M7GycXFxcHDxsPAxsbLxMzJzM/Qy9ri5+J6dFlYXFlMVlZaUl9Sbl1WUvteUFFlYGJSUmdeXmXf6ndja+/e4unY3+zj4+DW2t3W2uPW097g1dfb3tnbz97X089n8OP1YvpkdHRfUVxpUFBQVU9JS1ZOSk5QZV1cXthcWmN0U+Zgc2nXX+nd283M+8/J3Ovd5+vban7U6Fh9+Ol9bvTX7GLo4OxjZPrkalt0dVtWXHZtWWLl3nxv7udfT09eUVJRZmleX+nc7dzaz8/Y6tPU7d3T1dTY18vO6NHJ4vbg4NzoZnbhVU5aU0tUTU9cV0//flhf8GZrXlJcZE1OW1NLV1BWZl5c63Na/etfcOfu3tng0MnZ0sXFysPLzMG/z8/LzNTT5ODrZkxLRUFESUxUXG3l7WFnW0M7OzcxLzA0OT1GYNjJvbezsrGwsra3t7m8u728v7/AvcbS1czN2+vs6VhHQDw4NzlBS1/40NHbblNANiwpJiMhIyUoLDZG58i+trCws7Gzt7q4tri7trS0sa+2sK65x8LP6M/O3c7OamBPPDo+Oz5QXm/P1eXkY0A4MSomIyMkJiYsOENS1cO8t7W0sbS4t7m9ube7uLO2s7O2tLK9zMPLz9HO1cjsUkpGODg8PkNSWm7oaFpWQTQwKiMkJSIjKSwxQFfnwbu6srG1srCzsrO1sLC3t7G2uLq5t7nHy8fR3NbNz8x2VEo/Njg8PUJSX/HpY11OOjEuKCQlIyMoLC85RlThyr+4s7Owr7CvrrCwrrC0sLG0tLm3trrHv8TP0MrQz9dVRj42MTY6PkpdbOx8W0s+NS4rKCYkJCYpKzM7RV/bzsS8u7izsbCtrK2trK6wsrO3t7y8t7vFx8r/7/NsbulLPDo5ODg4OkVWYGRjXl9MPDIvLCkpKSgqLTA1PEZa3Mm+ubKurKysrK2tra+ys7O3t7u8t7i+wMHR1tje+fNaS0ZDPj5DR0hKS01OTEpBOzUxLispKiorLTA0OT9HU+fKvrewrq2rq6ytra+xtLW3uLe5ubq8xcjWblxcWFpZT0dDPTs6Ozo6PDw7PD8/Pj05NzQyMDAvLzAzNztCT//Nvrmyrq2tra2ur6+ytLa4uLe3tra3uLy/ydPb2d7ta1RJQTw4ODk3Nzo7P0ZJSEdEPjo2Mi4sKywtLzU6QVByz7+5s7Cvrq2trq6vsLG0tbW2t7q9vsLL2N769+Hb5f1bSkM9Ojk4ODo8PkVMT1BLQz46Mi4tLCwtLi80OkBS99DBubWxr66tra6vr6+wsbW3ury+w8PDyM3T2+ff4ftjTkM+Ozg4Ojs9QUZNV11cUUpCPTgyLi0uLzE1NzxCSFnkz8K6trOvrq6ur6+vs7W2uLm5vL69v8jO2u7l4XdgUkY/PTo6Ozo8P0FFSkxNTklDPjo2NDEvLy8wMjU7RE1l2Me8t7Ovrq2sra6vr7O0tba2tri7ur3EydHs6e1zZlpMREA9Ozs8PD4/REpOU1FMR0I+OjYzMTIzNTY5PEFNb9jHvbi1sq+vr7Cxs7a4u72+v77AwsDDys7X7fnscWJZSUE+Ojg4Njc6PD5ERklNSEJDPjk3NTEyMzI1ODxET3HSxLy2sa+urq6ur7Gztri4t7i4ubu8vsTIz+D08WlZUUY+PDk2Njc4Oz9BRUpLTk1HRUQ9NzUzMjQ3ODtASVjfyr+5tbKwr6+vsLCws7a4ubu7vL7AwsXL0NbmdWxnWVNMQDw5NjY3Nzk8PkNISElNRz89Ojc2NDExNDc8RFB20MW+ure0sbO0s7O0tba2tbW0tba4u77AwsjM0uXucltUTEI8OTc1NjY3Ojw9QEFCREM/Ozc1NTU0NTg9QklZ483Bu7m1sq+urq+vr7Czt7i5ury+v8HHzNTY4PNlVlNQTUpDPjk3NjY5Ojs7PUJHSkhCPz4+Ozg4ODk6PEJQdtrNx7+7uLWzsbCvr7Cys7S0tLa3uLq8wMTFxMvafV9TTUU/PDo3NDExMjU4Ojs8P0FAPj08PDs5NjY4OTo8Qk541svEvLaysbCvr66vr7KxsrO2uLm6vL/CxsrO1u5dT0pFQz49PDs3NTMzNDY3Njg7Pj8/P0BBQD07PD4/QUVJTlx93c7Fvru4tLCwr66tra6urq+xtLW3ur7Cw8jYbVpYUEhAPj49OjY0NDMyMjIzNTY4Oj0+QkRHS01OTkxOUlZeaOvZzMbCwLy6t7a1tLKwsrS1trW3ur7AwsnW4ejzcVZPSkc/PDs6OTk4OTk6Ojs8PT0+PUFFSklJTl1hX11249ze2s3JxMjHw768vb6+vLu9wL/Bv8HDxsjLzNPbfvJmXFJMTExLQUFCR0NDQkdDQD9BRUpISUxQUl1gbXDq3OLucN3b3//k18/R0s/Iw8fFycXFytDS1MzO18/OztTg9GvdemhNWF9kT1BLVlZNT01gXltXWFh2XVhOVmxqYF5j4uX1Z+vj2/pu5tXa+e3b1tfd8dz35eFt++Pi5m7reeHxYe3t6/hhaW5uYl5v/nZiWHpzYl5c/114YHj73N3n2+DW89vy5vXe91r87upp9Ozd6PHn7dfu32Lk9F9jWXJgcGNm53N6XGl1Z1peYGRlWVZVdWVgbHrZ+N3h1tfs2+je3N3f6Nvm3uHc4dvY7d7y4X3saPpZblp7ZfT9cm5483n+/OtsaV5fX2FeV/1dcFZpWmxXT0paWlBXamhyV2xs7WJ9Ye3m2+PY2fno6d7v3vTU4ujk1P7ieOThdvVx32rcdfN17N10d2vY4ONr5+zf+2Fm93JbVW7raV9s7mvvX+Hp6FjtZPdjbWRpZW/17+7Z2N/o6Nzl5V7nd+JnanjvWGZr7HJoZ+B+bVRwfvFfbv3e9HX23/Z9ZupyeGP74tvp9e56bmdfWnduYVxqXexr21bebdxfeWrY2Xv0eeBfZF36fGZsbPPi6/3k4N5gaWzi/PVo3dje4G5639le5N/X33Ra6+R0Wmje3P5r8t3eYl5f4/9wV3zm6V5Z3+z6Yfpl3WJrU3FeYk5bam9ZXFxl9l9Y+9Tk+tjU0uJ009reaNnu5Fnv3tl9buPi6V996PNpbNtm+VRlW2tWYP1jZlVcYG9oeF9r6un73unX1+pz0M74duHV6mte4dTh7OrX0OB639jp9Xxt4eFsWlNjeVhLWv57aVn60NhmZuXj6FVX7dr1a3Tn22VWa+vpW1le2X1yW2fl5VpY6eHxXFlf5F5OX+7db1750tdmaGT461hV5thsYG3b0XBU9/deVE5R6u5jZvzj3GRX5t3j/Gj60t1tat7e+l131OZje9vl+Hro19pcYOTy935y6Nj96tjb2tPf2s/e8N5scPlbUFhtbXdp8OV0aPxxb3peWFJTW15RXuTuZ/p87f1mZ+dtaPnxeXVpb3htZnBxa3RnbXBo797z5dPQ2OTy4dpsWX7lfmp26996euTb5Wxy6mxTXf56YV9z3trz8djY7X3w4OtzX+7gfXLw4d/i7+7b5Hds9ejseWd68G9bYXx4Zl1odGdfa27v6Xl07PtmYGF8+2pt6fJyd2/67WpheG9aVVRZXFRPZvp0c+DY5Ors2934Zmvu6mNe5dTsaOfZ5XhmfuDuYHDf42986N/q/P7p5fbo3Nvg3eLi4enq6nhz6vxdbujs+PPq5PBs/ex6amxoXltVX3tvXXLta19jYXttWlv6aF797+34YV50YFFaX1ZPT1Zkdv7f0tPZ1dbb3uDs7WpaX1xPUl1939vZ0MnMzMnH3HzBuNZRzLq8zd7Kv1Y0O0c9O0fewMXOvbx9QDw2LSYmKi0rL0BmZOC+t73Cvbi6w8a5tr29tbGyuLm1ucfNx9DxWVhcXUlDU/dWQD5KXU9DS2ZKOTIyNDM1RN3Iwrq1ucxdSz4wKisuLS83Snfcy7y4vcK/u8DNx7i3ubexrq+1tbK4wMDDytjo4NlgTltWTWV1Qz5JUk9OSFVYOzE2NTQ5RHzHxcC4vd1cTDovKyotLi84SVJh2dDEwM/txcXTyr+/t7W3sbC3t7e/xMfS0NB26dNwV2lRT1BFVNFPOEvsW1BMT2g/LzQ+ODtazr24u7SxxVlTRTUuLS82NjhR1dnRwb/ByM3Lw+HfvLzYxbGytbm8ubr378DK79DLztleTXNPPURVPz7+20o/YdzqT0pgfDwwNzw1OlLOv726tLncUko8LywsLTAzO05m5c3IxMPKzcfj0L7I7Ly0ube3u7a73c/AztzO0c7XXWHmTEdcW0tZP03LaDJM1E9Pb1X7ajU3STo2Vu/Uvr/At8hUVUo0LzAuMzg5SOj92cLDxMPMzcLLz76+u7e4ubCxubi4xcrUe+3f8erX7XL+XlRbSklQXT9F0+89P/dgV1FW//M5ND8/Nz9szsC/vLa9/llOOjEwLjE4OD9j997Kx8TBzM7CydvOxsbAvry3uL29u8DJy8/b2/xyem9bWFxWW1ZTYGpbRnnfTzpNX1FLS05cSjk9R0FAW9/MxcO/vc9cTEA2MzIyNTlAVOjgx76+wr/Jy7/L3Ma7yL63t7a0v8K8zOLN0fjb3H36/E1OUkZHUU4+XtxYQFjj+/9iaX5dPUBKRD9W38/Fwb++yW1YSjowLy8vMDU+Tl3pycHCxcXEx9HXz8rHwr25uLi4uLvAytHdc1tbXltXUldeXlpdXlpeS01tXD9AXVlOUFliZUk+SVRKS+3Oy8fEw8jcW09GOzY1Njk9Pk7u2dDCvL29wsPBxtTSxcTEwLy6ubq9vb/I1u5nVE1IRUdKS01SV1xjXV9qXk1LT05NTU5RW1xTWGjw5uDe3NTU3XlgVkxFQD4/QEJESVNje+TUzcrGwsLCwMDCxcbHxsPDxMbGxsjLztje7WhYVFFNS0tMS0lJTE5MS0tNUFBPTk9RUFFVWVxeanB1e31waGBjY2BganNvdPTw6+Xf29nY1tbY19nc3uLr8Ozo4Nvc2dXS0c/S1tfb4ejs8+zw7u7r9Hh3eH7y7url5vJ7eGliZmRjanf8/f7z7u/x8v11dW9mYl9aWFlXV1pbXWBkZmdqbWhdWVpaW19udW5z9uns+37783ZiXFxcWlpdY2tsbfLh4eHe39/f3uPr7+/k39/f3t3d3Nrc3uTq8Xp1b21rZWJlaGJhYmlzfP316uXp7eXj6f377uzw8O7y8uzt7vB9d3NtZWJeX2ZoaGdpcnRvcnFwcGxra2dobG1z++vn5+3j4N/d5e96fnhuZ21ze37v6uvq4+Pq7n12amdjZ2lqa3B38uzq4dvb2dXX2dzd5+p3a3V5dnR9e3d6/n51bGxpX1xaWl5gYGBpdX708/16enNwdXd0cm1sbW5ubG5z/nJvef57b3JzbWtkZ25mZmptcHx2dXV3dHX97Ort5+Xn7PDx7+/q5+zw8+nh4N7f3t/e3+Hm6evr7fL5+31zePjy7vD79vj8+Xl+eWlpbG96dW1xdnNvbW549+75d25sb3r47OXn5eXn4ubt8O7s7O3v/H53dm9tdHNufXZuaWFeXF5eYV9dXWBgYGdmaGxnZWVoe/j98/T/d2tjZ2toZmdtcnZ6/fn58+z08evy9/p+e3f+fPDr6+Xi393d3NrZ2tze3uDh4uDb297g5Ofs+3NrZmJmb3V+enp0ZmFfX2FhYWNlaGlgXV5iZ25wdPfy9O7l4eLl6e309/Z3cHd++v59/Pp9dmtte31+/ff0+ff9eHFtbGttb3B6+vf9/nh6eXJua25weHBucG10dnB1enn29/Lp6erq5d7e6Ozn6e3r7e77cm5za2Zobvzu6+vo5efp7PV2cnF1fHhuaGFaWltbXGBrb3h8++/0+Xt4//r2/P75fWtoamlobXzs6ubn6+zq7O3t7/D8/fj7/fr87/Ds4eXr7fH4fXd2/fn17urn6ert7uzs7uvm7PDu+HJtamtxeXpzeXZ0a2NiYWRlY2VoZWFgZm97fvjt6uzv7uvq8n39/nFobGtra2xuc/ns5+jp6/D5+Pt1fn14dm1obn37dG5ta2xucHR5bmppZmpmZersZF126NxxXWhsVlds+ujfa3bc3u/8cfjc3vRlV1FcZf3azszJyMrK0uf66ePd1t7p6Orm4OT8Z2h7dm9fW1hYY29sZmFhbHRxXlpUUFJWWFdWVFpr+Pf2fOzd3t7k7e3g5eXe3N7d2dvV1ePp5Ofk53BebXn15+zs6ezl2Nfg5+bh2Nn6XlJNU2Fu/PZza3d3b2pYUE9KSUdCQT9BRkxPU1Ji4NDMy8zSyt7Vu7bA1GVYwLCwuvM1LzM5VNXHurKytL5VNSsnKCssLTA0O0pQTkhBR17MvLe4ta+sqauvtLm5tbW0tbe1s7jA11tQaO5bT0tmZ1I8Nz73w8VfMCQhJS1GzLeurK621T4tJycrMDg6Oj0/PTw5NTtK5cO9vbmyr6+zvMK/ubKxtLW3ubm9wMfVYXFdVGzc21g+NUHevb9eMSckJzA+cMO4tLjITzYsKi41OTs6Oz4/Pjk4OUT9xrmzs7a0sq+trrK2t7SxsrS3ubu+ytPX4unc9FR718bZUTg7VsS82D0rJik0Sde/ube7y1c5LSsuNz9AOjU3NTg6ODY4SNS3trnCwru2t7q6ubGvsLa8wsLDwMDK3mVUXnNYa9rOekM2OlbIwnA1JyQnMkrUv7u4usRuPjAuMj5NTUE5NjY4MjY8Ql3UwbextrrAvbi0sq+urq2ws7e6u77GyM3Uz9/zZk5V1Mv2RDQ3Uc3C2zsrJyk1TtvGwb68xH0+Ly4zPU1JPTUzMzo7OTtCXs7Av7+/vbm4uLm6u7u5uLe3urzAwsbLz9vtbHpiau3Ozlg+N0HevsRXMCkoLTxb0sW/wMfdSzcvLjZCTkg9ODQ4NTlBRU1jyrius7vJx7q0tLa2trCytrvDyMnIyMrc6P5edFlc+dVsSDk7X8m+0EMuKiw2TOrJwr69xv5BMi80PU1RRjw4NTk6OjxDWtHCvLm5uLi6vLy7uLa0tbW4vb/DxcnO0drh4uNrau3P2087OUnTvs5FLikqM0Zt1MjAwcn2RDUvMz1NVkg8NTQ1Ojg7RFjVysC7s7O3v8K8t7Kxr7Cwtbm/ys3Q0tLa5Xd8UEhMX+lOPzZDfMHIVjQrKzBBXdLKw8HI6kg3MTE6SlhNPjg2OTs7OTtL3MG9vLu2srO0t7i4tbSztbi6vsDCxcrP1NLSyc7h5dPM7kQ2O1PMyVw2KyktOEdg7tXJzOBOOzIyOEBMRT02MzY5ODc5QnDLv729ura0tLa5ubi2tLW4u8DDw8XJzc/R2N/ncfPo2flMPkBez8xiPDAuMzxJWezXy8vdUTszMjg+QT06NzU4NDc7PkRSzrixtbu9urGwsLO0sa6us7nBxcbExMvU5v1w+2xpd+/oVkNAS/3Yaj8yLC0yPEpd3szFyuVMPTo7RU1HPjk4Ojo4Nzk/UuDEura0tri3tra3ubq4t7i6vcDCxsnN0dTa2dLo+/He+04+P07h2WM9My4vNDtDVvPPzuZRPjg5PEBCPjw7PDo2OTo6PUrXuLO4v7+2rq+0uru0r7G2vb67vMDFzNHX3uj4aVtWW2tYSD5EWdvcWTwzMDU7Q0pn1MXDz2VIPz5AREM/PT49PTs6PEBL+83DvLm3tre4ubm5ubm3trW2ur2/wsbK1t7g3u75/OX+UkA/SGX2WkA1Ly4vMTc/W9nO1G5QSUVAPz5AQkRAPTo2Oj5FRlXWu7Oyt7m4tri6vLm0sbS5vMDDxsvO09jX3t33bGj9YExBP0hebVZDOjU0NDU5Q17a09xzWk1IPjw8P0A/Pj8+PDw+RU1d2sK4s7KysbGxtLa2tLOztLa4ubm8v8PHzM/W5Pr+/ltIP0JNX1hHPTk3NjY3O0RTbXZjWlFLRT8+QUI/PTw+Pj08PUBIV+zRyb22srKztLa3ubq7vLy9vb7Bw8fL0N3l8P5nX1ZRS0VER0pHQDs5Nzg4Oz5HT1deXl1YUEpHQ0A9PDw+P0FFR0hQctvLwbu2s7CwsLCytLW3uLq8vcDExsfKzs/Q1+Z3Xl1aUk1KRkZHSEZEQ0JCQkFCRERHS09VW2RsaGFfWk9JTU9JSktMWGBp5tPKw7+9u7m2tra2t7m6vb/Eyc3R2ODv/21hWldSTU1KRURHSEhJRkRDQkJAP0FDRkpQWF5gXllWT0tIRUNDQ0VHS1NbaOXUy8fAvbu5uLi4ubq7vL7Aw8bKztHX2eDr9H1zaF5cW1pbXFtUTkpIQ0A/Pj9BRUhMVV5oe/f+a2NYT0xLS0tLTFVn7eDXzca/vLm3tra2t7i4ury/xcnM0d/r9ft6bV5fYFlVVFJPT01LSEVBQEA/Pz8/QURHS01PVFZTT05NSkpHREVITE1OWG3z29DJwr69u7q6urq6vL6+v8DDyM3V3+r+cmtjYV5aWVZTUE1NTEhFREJAPj4/P0BDRktNTVFYXl9eWFhcX2BmbfHj2dLMyMTAvry7ubi4urq6u7y+wMPFyMvR2d/q7HRhXlpRTk5MSklJR0ZGRUVDQkNEREZHSElLTlBSVFVSUVBTWVtbXF9u9+fg2dHLycbDwL6/v8C/v7/BwMLGyczT4Ot5aVxWT0xKR0RCQUA/Pz9BQUNFRkVHSUpMTlFVWl9kaHB39Ovu4tzY1M/MysjGxcG/v7++v76+v8DBxMfJzdHZ2+Ho6/lsXVRNS0hGQj8+Pj5AQEBCRUdKS0tOT1BRVFhcZGtzc37t4NvV0M/PzMrJycfFxMTDwsLCw8PFyMrLztTa3d/tdm5nXFdRT01LS0pISUhJSUdFRURGR0ZGSEpKSkxOVFthaHf46+ro4d7f3tnZ2dHNy8rIx8XExsfIycvNzdLW2t/k9m/8dGllaGRgaGldWFVVVlNQT1FSUU9RWVdUVlZYXFxeX1xfZWZkZm5xfvb37Ofe2NXU0dHNy83Pzs/Pz8/Ozs7Pz8/Pz9PZ3d7e5vj68G5oX1JPUFFPT1JVVVhaXl5lamdma3BrZmFndXd2/Pjo6/p9eXJyZ2NlZW796+7q5uTj4d3b2eDw9H1oXldUVlZWXV1eX2RlYGRdXmhz8u7x+XNqbGhkY2VgZGNjY2hvbXTu7u3l39/n83puaF5fa+7j3tjX2NjX19fb29jV193e2t/r6ubm5ePh3uTs/XV7/Hny6efuempeW1xcXF9qfO3q5+rs6fV1cXl2a2xub2llaHB6dPbu5ev4+25jZF9hbXZxfu71d3h2/Xx0e/z3eHJ7c2xubnJrbXJ683htbGtpZ2x+8uvu5eTy+Pt0bGRgZ2tjYWpraGxub/f57erp5+bq7Orq6ubk5OHk3uDq5+jl9n3/cHF0aXL2enz56+n19fHt7v39/frs7erk5uzo7X34eHF+b3Pz6vD+7OTj5O357/R2bnt4bGZmaHRubfz17/Hw6Orw/nBqX11ZXWJna25zeHX+8/Xz7eXs8/lya2FcXFxaWFhdYGtpY2llX19ja//z7e7p5uDi5eDf39/h5d7k6+zs6uHg6d/a3eLufPP3/f97emthW1pZWFxfaXV8/fb4d3BnYmJja3Nw/+3o6e91dnpsZGhua21obHvt9Ojd29jX1tPW2djb4/V4cGtjZ2BhYmVu/+ne4Ovm6uzr7vn9fnVuaWdsdWtnam1sc29veHBrdHVramJrcXJsbXl2cfh99O7+a3V1cXzy8/PwdXd88uzze3Jra3jz+/bwdnZnXmhxbm54+Xp5/P/6fff48+jt+v95dWplYl9jbHTz7efp6t/o4dre4eHd3d7f5+Xod2prZmdlZm19enR9fPp+dnj9fXF0/O98cHFqaWVmaW1xbG1vbGRrcHT++/jw7PPp6ezw8+t9ffLu7O/47OLm6ePo7vP7+ersfXB18Orm397d3+Tg4/h7b2VaVlhXVVZYXV5dXV5od3758Pd6fPfw9HVwbmtue3t1d3BobWpqa2x3dXz39/r48/DvenFvdHN9/Pry+nt2bW1saGt4dn3w5+Li4Nzc3+nq6efl5+fj6vDu9PLt8ers8+3o6e3t5+Dh5O/v6ev6dmtmYl5eY2VhZXZvdHF59evr/P/88/b9fvTt9vp5dfL07vn/9O7q8PHx+npqZGFgXVpcXl5eYmZnZnH5fXVpbm9saX3y9/bu6+79cXV7bGRiYWdrampvffV8ePv47vL7/PLt+m1samtvbXX19Ozz/O/w8vz07e7r7fn8enN88Pv44t3i5ODc3eDm6OXn9/To5urs6ubs/fvo5PD5+nd+9/f6/+7u+nJwb2pkYmZla3Z5b258+vf9dnx+d3v/dnR0bm1pY2BlYmBs+/r17+vq6Ovw7/t+b2hlZmFfaG5oY2ty/fl99OzveG5ub2ZkamxpbHB2/f728ujm5eXv8PV6amVoZWNvdXby7Ovs6eXh39/g4ODh3+bo6Ovw9frw7vL6/fp1d3Ry/vn8bXR+eXRxbnF0bm1z9/Du7urf4uTi4eDi5+rt/XBjWlZZW1pcZXB1+e3q4t/e3+Lo7vt3bmtkXl5jZV9fZ3F7cnH+/PDu8O7v/nB3ffr39PLw6+zn6ezm5ebt6+Xub258ffb6cm10//Hs8vp4eHVtaGlybWdgXV5jY2JjaGx2d/vz+fV4bGdsZGNmX15cW1xeYGVtbnB79X53dn759vL68Onn5ufd29XQzs3NzM7S1tvf4uzl5+jp9vT5cG94eH78enf68fl5dHJ3emxnYlxYVVZZWV1obf7z5uXr7O7t7+7q6erq/3R5dG9tbW5sbnr9dGxqdPj69erm5eTt7Pp2bGVkX2JrdHZ6dvXv/P38bmNcWltYWltaWVpbWFhcXWJtcvHr7eji3t3c2dbW1tfa4O769/z1fXZ4+vH17+/18PB9+vDp7fT48+7u5eLh3+Tn6ujl5+zv7vP08Px5+PTy8Oni3eHt8PTs5ODi4N3g4uLj7O/5a2doZ21sZ19cWFdZWVlaXmNkaGdkaWxqaGdkYV9fXFlaWFhZWlhaW11fXl5jZmxucPvz+nRyeXRpbH72+/768Ofg4OTh3dfW2tnX2Nvd4N/e3+Df4efk4uDe3d7h4uTk5d/e5vT39Hxvcnf67ufm5ePi4eXm7vJ8bm9nZ3N6eXV2cHt6cHBwbGJiZWBdW1pcXWBob3r78uz29/R4cHJva2JcW11aW1xdYWNmbHN79/P2+358dnl9e354c3NvbGxnaXT+//Xt7Pf4+O7y+vDv+Pbw8/j9e3dvaWt1+f307/X5935+9Px0ef708/Dt7evs7+jn4d7g49/j8fv09fbs5+bk5ujo6ujl7vr3fnx4/vb6fHhya3T77erp7O7s6ufp6+7y8/L5+/T6/P//en39cmxqam9vbnR9+Hx29Orl5uTk5ex8c25rZmBgX19hZmxucnFta2poamZhXl5dXV1ZWVpcYWVkY1xaWVhXWVtdZG757erg29vb2djZ29/k5uzu7v14dHV2e/Xv7vDr7P9tb3zz8/rx7Ovn4+Xt9fPu7O7u8fb49vX7//14bmtnbnJtaG1va2ZlanB3fPzv6OXk397f4t7b29zc2tnZ2dve3d7k6Obk5Ofs7/N7bW5ycnBwdP/1/HV1enp6cm9sZWJhYWBeWFlbW1lXWVxcXV9kam1ua2trb339enVuZmRqaW11fHV9+v38/vv2+/v7enBvcG59/vf3/v1+eXz3+fbx7vD8cmtsc3j47Ojo6Orz9vXo39/c3un0eXNybGVhY2dpanV8dX3y6ePo8ntzePrw7Ozq6PDz+P398ubd3+rv+PV9bnBwbm15/vL1+fx++/Do5ejo4+Xr6+rt8/f27+zu+/7y+XRucm9qbWxqa3B4fHx8c29vev1+fnx+/Pnx+Xh3eXZ88/Hr6Or4dWxqb3Nvb3l+dG9wc3Jua2ttbWttdXl9cG5sY2BdW11iZmJna2xwdXn06+vs6efn6uvt8f14ePn5+O7t9Prx8/Pt7vV9c3RycXNxefju6ubk5OTl6fD6/P14bnL9/fXo4uTs7enl6e/z9PL57+jm6Obl5OTo8/x2a2BdXlxZVldcXV1iZWpuevLr7u7t7+3t+25lY2dwfX5+bWhkY2Ruffvs6ejl5ebq9/fv8Pp9/XZsZ2psamlmam9pZWp0/vrv7evt8Pp7dHX67Ovr8Pv69Pn06OLh5+jq6uno7O/x8/L29/L0+Pz28O/u7Ozs8vjo3+Di4N7d5+vk3+jw8XtvbHJsbHT77O7x+fd+/XdzeHhybWlrbmxtamdjYV5aW11dXFxfYWlra214/fXu7ebk6Ofp7ff9e3lwaGlsamdqdHBubHNwbGloaWdlYWNmaWpvcHB1dXdxd/t9cG5sbm1sa29taGtxcm1vffX7/vXv7vl0df5zbnr9/v308+7t7OLd29XR0tXY2dvh5+nwd25ub3FvevD29vDt6v597ers6eHd2tvf4N/l6efm5Ovt6O3s5OLi4+fv/HVwbWlte/b3/PXz+3Fsa294bWt3d3Fram9pZG/++/Pq6Ons8Ph+dW5pYFlYWFdYWltXVlVVVFVVV1hYWFxgYGNiYV1dYWxwcHD99vf49vD0+/v+eXF78vb38/P2+fjr4uDk4d7k3tvZ3N7d2t3f39vd4+Hg5urm4OTl5ezweWpjXlhXV1lZWlxhaHR8/+7p5eXm5uXj5enr6Obu/fLu8vPw4OLn7Onq9/fv6+nn4t/c2tjY2Nrf4efu7u3q7uPe3NbY2dbY4Ofs8fr/enhxbmBbW1pVUlRWV1RRVVtdWVdZVlRTU1RVWFlcXF9jZGVgZGJdXV5laWtpaWRgYWFpbm9vfvn67+7x8fxyee/x8+7m4d7g39va2tre3+bv7uvv8PDu6Ovu6OTf3N3e4OLg39/i5OPp7e7x9fl4dXn06+zi3t/f4OLu/HhtcHh1b21tbnBydH7/b2dnbnVvc/zy8Pbu7uzt6+vr49/e3N3e6/b+fHR2bmx0/vL2/HRwa2JfYGBfYF5jZmhoY19cWlpbXF1bW11eX19oa25qZGNkZGZkYmVt//Dr59/d3uj17uTj5evu5+Pj6fDu7ezo5ePg3N/l6ezs9vTt5+Dg3+He3N7i4OHi6fh6fnJtcXh4cWxsaW1xc3r16ODj5OPp83749/nv6ubt+nBv+O3v7u/u8Xtwbf70+fb/+vh0al9cWlpbX2p86ubk4+jn5urt7vpubm1lYmFiZGtkYWxnXVdTWFtaXWFna2xqbW9tbWtre/Lz+/349fb0+vTvcXJt+vb68u7u7Pd7/v/+fn5+fn59AA=="
                        };
                        await SendToWebSocketAsync(openAiWebSocket, second);
                        
                        await SendToWebSocketAsync(openAiWebSocket, new { type = "input_audio_buffer.commit" });
                        await SendToWebSocketAsync(openAiWebSocket, new { type = "response.create" });
                        
                        var response = jsonDocument.RootElement.GetProperty("response");

                        if (response.TryGetProperty("output", out var output) && output.GetArrayLength() > 0)
                        {
                            foreach (var outputElement in output.EnumerateArray())
                            {
                                if (outputElement.GetProperty("type").GetString() == "function_call")
                                {
                                    var functionName = outputElement.GetProperty("name").GetString();

                                    switch (functionName)
                                    {
                                        case OpenAiToolConstants.ConfirmOrder:
                                            await ProcessOrderAsync(openAiWebSocket, context, outputElement, cancellationToken).ConfigureAwait(false);
                                            break;
                                        
                                        case OpenAiToolConstants.ConfirmCustomerInformation:
                                            await ProcessRecordCustomerInformationAsync(openAiWebSocket, context, outputElement, cancellationToken).ConfigureAwait(false);
                                            break;
                                        
                                        case OpenAiToolConstants.ConfirmPickupTime:
                                            await ProcessRecordOrderPickupTimeAsync(openAiWebSocket, context, outputElement, cancellationToken).ConfigureAwait(false);
                                            break;

                                        case OpenAiToolConstants.Hangup:
                                            await ProcessHangupAsync(openAiWebSocket, context, outputElement, cancellationToken).ConfigureAwait(false);
                                            break;
                                        
                                        case OpenAiToolConstants.TransferCall:
                                        case OpenAiToolConstants.HandlePhoneOrderIssues:
                                        case OpenAiToolConstants.HandleThirdPartyDelayedDelivery:
                                        case OpenAiToolConstants.HandleThirdPartyFoodQuality:
                                        case OpenAiToolConstants.HandleThirdPartyUnexpectedIssues:
                                        case OpenAiToolConstants.HandleThirdPartyPickupTimeChange:
                                        case OpenAiToolConstants.HandlePromotionCalls:
                                        case OpenAiToolConstants.CheckOrderStatus:
                                            await ProcessTransferCallAsync(openAiWebSocket, context, outputElement, functionName, cancellationToken).ConfigureAwait(false);
                                            break;
                                    }

                                    break;
                                }
                            }
                        }
                    }

                    if (!context.InitialConversationSent && !string.IsNullOrEmpty(context.Assistant.Greetings))
                    {
                        await SendInitialConversationItem(openAiWebSocket, context);
                        context.InitialConversationSent = true;
                    }
                }
            }
        }
        catch (WebSocketException ex)
        {
            Log.Information($"Send to Twilio error: {ex.Message}");
        }
    }
    
    private async Task ProcessOrderAsync(WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context, JsonElement jsonDocument, CancellationToken cancellationToken)
    {
        Log.Information("Before extract ordered items: " + jsonDocument.GetProperty("arguments"));
        
        context.OrderItems = JsonConvert.DeserializeObject<AiSpeechAssistantOrderDto>(jsonDocument.GetProperty("arguments").ToString());
        
        Log.Information("Extracted ordered items: {@Items}", context.OrderItems);
        
        var confirmOrderMessage = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "function_call_output",
                call_id = jsonDocument.GetProperty("call_id").GetString(),
                output = $"Please confirm the order content with the customer. If this is the first time confirming, repeat the order details. Once the customer confirms, do not repeat the details again. " +
                         $"Here is the current order: {{context.OrderItemsJson}}. If the order is confirmed, we will proceed with asking for the pickup time and will no longer repeat the order details."
            }
        };

        context.LastMessage = confirmOrderMessage;
        
        await SendToWebSocketAsync(openAiWebSocket, confirmOrderMessage);
        await SendToWebSocketAsync(openAiWebSocket, new { type = "response.create" });
    }

    private async Task ProcessRecordCustomerInformationAsync(WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context, JsonElement jsonDocument, CancellationToken cancellationToken)
    {
        var recordSuccess = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "function_call_output",
                call_id = jsonDocument.GetProperty("call_id").GetString(),
                output = "Reply in the guest's language: OK, I've recorded it for you."
            }
        };
        
        context.UserInfo = JsonConvert.DeserializeObject<AiSpeechAssistantUserInfoDto>(jsonDocument.GetProperty("arguments").ToString());
        
        await SendToWebSocketAsync(openAiWebSocket, recordSuccess);
        await SendToWebSocketAsync(openAiWebSocket, new { type = "response.create" });
    }

    private async Task ProcessRecordOrderPickupTimeAsync(WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context, JsonElement jsonDocument, CancellationToken cancellationToken)
    {
        var recordSuccess = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "function_call_output",
                call_id = jsonDocument.GetProperty("call_id").GetString(),
                output = "Record the time when the customer pickup the order."
            }
        };
        
        context.OrderItems.Comments = JsonConvert.DeserializeObject<AiSpeechAssistantOrderDto>(jsonDocument.GetProperty("arguments").ToString())?.Comments ?? string.Empty;
        
        await SendToWebSocketAsync(openAiWebSocket, recordSuccess);
        await SendToWebSocketAsync(openAiWebSocket, new { type = "response.create" });
    }
        
    private async Task ProcessHangupAsync(WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context, JsonElement jsonDocument, CancellationToken cancellationToken)
    {
        var goodbye = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "function_call_output",
                call_id = jsonDocument.GetProperty("call_id").GetString(),
                output = "Say goodbye to the guests in their **language**"
            }
        };
                
        await SendToWebSocketAsync(openAiWebSocket, goodbye);
        await SendToWebSocketAsync(openAiWebSocket, new { type = "response.create" });
        
        _backgroundJobClient.Schedule<IAiSpeechAssistantService>(x => x.HangupCallAsync(context.CallSid, cancellationToken), TimeSpan.FromSeconds(2));
    }
    
    private async Task ProcessTransferCallAsync(WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context, JsonElement jsonDocument, string functionName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(context.HumanContactPhone))
        {
            var nonHumanService = new
            {
                type = "conversation.item.create",
                item = new
                {
                    type = "function_call_output",
                    call_id = jsonDocument.GetProperty("call_id").GetString(),
                    output = "Reply in the guest's language: I'm Sorry, there is no human service at the moment"
                }
            };
            
            await SendToWebSocketAsync(openAiWebSocket, nonHumanService);
        }
        else
        {
            var (reply, replySeconds) = MatchTransferCallReply(functionName);
            
            _backgroundJobClient.Schedule<IMediator>(x => x.SendAsync(new TransferHumanServiceCommand
            {
                CallSid = context.CallSid,
                HumanPhone = context.HumanContactPhone
            }, cancellationToken), TimeSpan.FromSeconds(replySeconds));

            var transferringHumanService = new
            {
                type = "conversation.item.create",
                item = new
                {
                    type = "function_call_output",
                    call_id = jsonDocument.GetProperty("call_id").GetString(),
                    output = reply
                }
            };
            
            await SendToWebSocketAsync(openAiWebSocket, transferringHumanService);
        }

        await SendToWebSocketAsync(openAiWebSocket, new { type = "response.create" });
    }

    private (string, int) MatchTransferCallReply(string functionName)
    {
        return functionName switch
        {
            OpenAiToolConstants.TransferCall => ("Reply in the guest's language: I'm transferring you to a human customer service representative.", 2),
            OpenAiToolConstants.HandleThirdPartyDelayedDelivery or OpenAiToolConstants.HandleThirdPartyFoodQuality or OpenAiToolConstants.HandleThirdPartyUnexpectedIssues 
                => ("Reply in the guest's language: I am deeply sorry for the inconvenience caused to you. I will transfer you to the relevant personnel for processing. Please wait.", 4),
            OpenAiToolConstants.HandlePhoneOrderIssues or OpenAiToolConstants.CheckOrderStatus or OpenAiToolConstants.HandleThirdPartyPickupTimeChange or OpenAiToolConstants.RequestOrderDelivery
                => ("Reply in the guest's language: OK, I will transfer you to the relevant person for processing. Please wait.", 3),
            OpenAiToolConstants.HandlePromotionCalls => ("Reply in the guest's language: I don't support business that is not related to the restaurant at the moment, and I will help you contact the relevant person for processing. Please wait.", 4),
            _ => ("Reply in the guest's language: I'm transferring you to a human customer service representative.", 2)
        };
    }
    
    private async Task ProcessRepeatOrderAsync(WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context, JsonElement jsonDocument, CancellationToken cancellationToken)
    {
        var repeatOrderMessage = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "function_call_output",
                call_id = jsonDocument.GetProperty("call_id").GetString(),
                output = $"Repeat the order content to the customer. Here is teh current order:{context.OrderItemsJson}"
            }
        };

        context.LastMessage = repeatOrderMessage;
        
        await SendToWebSocketAsync(openAiWebSocket, repeatOrderMessage);
        await SendToWebSocketAsync(openAiWebSocket, new { type = "response.create" });
    }
    
    private async Task ProcessUpdateOrderAsync(WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context, JsonElement jsonDocument, CancellationToken cancellationToken)
    {
        Log.Information("Ai phone order items: {@items}", jsonDocument.GetProperty("arguments").ToString());
        
        context.OrderItems = JsonConvert.DeserializeObject<AiSpeechAssistantOrderDto>(jsonDocument.GetProperty("arguments").ToString());
        
        var orderItemsJson = JsonConvert.SerializeObject(context.OrderItems).Replace("order_items", "current_order");
        
        var prompt = context.LastPrompt.Replace($"{context.OrderItemsJson}", orderItemsJson);
        
        context.LastPrompt = prompt;
        context.OrderItemsJson = orderItemsJson;
        
        var orderConfirmationMessage = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "function_call_output",
                call_id = jsonDocument.GetProperty("call_id").GetString(),
                output = "Tell the customer that I have recorded the order for you. Is there anything else you need?"
            }
        };
        
        context.LastMessage = orderConfirmationMessage;
        
        await SendToWebSocketAsync(openAiWebSocket, orderConfirmationMessage);
        await SendToWebSocketAsync(openAiWebSocket, new { type = "response.create" });
    }
    
    private async Task HandleSpeechStartedEventAsync(WebSocket twilioWebSocket, WebSocket openAiWebSocket, AiSpeechAssistantStreamContextDto context)
    {
        Log.Information("Handling speech started event.");
        
        if (context.MarkQueue.Count > 0 && context.ResponseStartTimestampTwilio.HasValue)
        {
            var elapsedTime = context.LatestMediaTimestamp - context.ResponseStartTimestampTwilio.Value;
            
            if (context.ShowTimingMath)
                Log.Information($"Calculating elapsed time for truncation: {context.LatestMediaTimestamp} - {context.ResponseStartTimestampTwilio.Value} = {elapsedTime}ms");

            if (!string.IsNullOrEmpty(context.LastAssistantItem))
            {
                if (context.ShowTimingMath)
                    Log.Information($"Truncating item with ID: {context.LastAssistantItem}, Truncated at: {elapsedTime}ms");

                var truncateEvent = new
                {
                    type = "conversation.item.truncate",
                    item_id = context.LastAssistantItem,
                    content_index = 0,
                    audio_end_ms = elapsedTime
                };
                await SendToWebSocketAsync(openAiWebSocket, truncateEvent);
            }

            var clearEvent = new
            {
                Event = "clear",
                context.StreamSid
            };
            
            await SendToWebSocketAsync(twilioWebSocket, clearEvent);

            context.MarkQueue.Clear();
            context.LastAssistantItem = null;
            context.ResponseStartTimestampTwilio = null;
        }
    }

    private async Task SendInitialConversationItem(WebSocket openaiWebSocket, AiSpeechAssistantStreamContextDto context)
    {
        var initialConversationItem = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "message",
                role = "user",
                content = new[]
                {
                    new
                    {
                        type = "input_text",
                        text = $"Greet the user with: '{context.Assistant.Greetings}'"
                    }
                }
            }
        };

        await SendToWebSocketAsync(openaiWebSocket, initialConversationItem);
        await SendToWebSocketAsync(openaiWebSocket, new { type = "response.create" });
    }
    
    private async Task SendMark(WebSocket twilioWebSocket, AiSpeechAssistantStreamContextDto context)
    {
        if (!string.IsNullOrEmpty(context.StreamSid))
        {
            var markEvent = new
            {
                @event = "mark",
                streamSid = context.StreamSid,
                mark = new { name = "responsePart" }
            };
            await SendToWebSocketAsync(twilioWebSocket, markEvent);
            context.MarkQueue.Enqueue("responsePart");
        }
    }
    
    private async Task SendToWebSocketAsync(WebSocket socket, object message)
    {
        await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message))), WebSocketMessageType.Text, true, CancellationToken.None);
    }
    
    private async Task SendMessageToOpenAI(ClientWebSocket ws, object message)
    {
        string jsonMessage = JsonSerializer.Serialize(message);
        
        byte[] messageBytes = Encoding.UTF8.GetBytes(jsonMessage);
        
        await ws.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }
    
    private async Task SendSessionUpdateAsync(WebSocket openAiWebSocket, Domain.AISpeechAssistant.AiSpeechAssistant assistant, string prompt)
    {
        // var configs = await InitialSessionConfigAsync(assistant).ConfigureAwait(false);
        
        var sessionUpdate = new
        {
            type = "session.update",
            session = new
            {
                // turn_detection = new { type = "server_vad" },
                input_audio_format = "g711_ulaw",
                output_audio_format = "g711_ulaw",
                voice = "alloy",
                instructions = "你是moonhouse的助手",
                modalities = new[] { "text", "audio" },
                temperature = 0.8
                // input_audio_transcription = new { model = "whisper-1", language = "zh" }
                // tools = configs.Where(x => x.Type == AiSpeechAssistantSessionConfigType.Tool).Select(x => x.Config)
            }
        };

        await SendToWebSocketAsync(openAiWebSocket, sessionUpdate);
    }

    private async Task<List<(AiSpeechAssistantSessionConfigType Type, object Config)>> InitialSessionConfigAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistant, CancellationToken cancellationToken = default)
    {
        var functions = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallByAssistantIdAsync(assistant.Id, cancellationToken).ConfigureAwait(false);

        return functions.Count == 0 ? [] : functions.Where(x => !string.IsNullOrWhiteSpace(x.Content)).Select(x => (x.Type, JsonConvert.DeserializeObject<object>(x.Content))).ToList();
    }

    private object InitialSessionTurnDirection(List<(AiSpeechAssistantSessionConfigType Type, object Config)> configs)
    {
        var turnDetection = configs.FirstOrDefault(x => x.Type == AiSpeechAssistantSessionConfigType.TurnDirection);

        return turnDetection.Config ?? new { type = "server_vad" };
    } 
}