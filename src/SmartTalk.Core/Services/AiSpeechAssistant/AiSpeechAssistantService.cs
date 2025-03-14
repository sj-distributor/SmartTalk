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
                                audio = "UklGRgBXAABXQVZFZm10IBIAAAAHAAEAQB8AAEAfAAABAAgAAABmYWN0BAAAAKtWAABMSVNUGgAAAElORk9JU0ZUDQAAAExhdmY2MS4xLjEwMAAAZGF0YatWAAD//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////37///9+/////////////////37//35+////////fn7//37///9+fv//fv///////////////////35+fn5+fn5+fv/////////////+/v79/f7+/35+fv//fn19fX7///9+fv7+/v79/f7+/v39/f99e3p5eXp6enp6enx9fv9+//7+/v79/P3+///+/v5+fXx7enp7fHx8e3x8fv78+/z7+/v6+fn5+fn5+Pb09fX19PT09vj5+/v8/P39/fz8/n59fn3//f5+fXx5eHd0c3JycnJ0dXZ3eXl4eHh4eXh4d3Z3eXl5e3t6eXp7fXx8ff77+fr7/Pz8/P3+fX18e3t9//37+fn8/P3+/v7+/v99ff/+/Pz+fHx8fH59e3l5en78+fb08vLz9fX3+vr8/n59fX7++fj7+vXz8vP09fb29/n7/37+/v3+fnp2dHRzc3V3eHV0dnl9/v97eXl8ff79/Pv8/Pz7+vt+fHp7/fr49/j4+Pf48/L1+Pr8/Pn7/X16e/79/n59/n55eXx8eXd1c3N3dnRzcW9sa2prbG1ubm5wcXJ2eXt+/v7+/fz69vb49/Lv7evs7e/w7+/v7u7w9ff08O/u8PHw8O/t7fD19vf4+Pj+e3h3eXh3c3Jxb25wcnBwc3Z8+/Tw7/D5fXp4dnd0cHFwcXZ8/Pj4+vz/fv5+fHl4c3BvcHJxb25vcHBwc3J0eHp7enz8+/5+/v1+fX18fv99e3p4eHZ2ev34+vn19vHu7uzr6+rr7/T18/Dz+Pn5+fT09PX3+fr7+fj6+v17eHd7/P59enZyb29vcG1samlsbG1wdHV6fXd2e/7/fH758/Py8O7w8/T28/Hw8O7u7u3t7vLz8/j7+/f5fXt+/n14cnBwcHBwdXZ2enp0bWtrbWtra2tucnV4fHp4c3V0eH7+/nz//fv18O/w9P18dXJzb21sbGxvdXv88/Lw7ezr7vT3+/7++/f39/j37u3u7+7y8/Dy9/j4+Pn4+/z9/P39/fv8fn7//nl0b25zdXV5eXd2eHl+/fr7eXJzfXt7/ntxcXh7+e7r7O/x7+rp6+vv+Pv2+Pv6/nh1cnJycnBtbG1vcnd7fn58dnN6fHlxbm93ent7/fr8+fPv7Orq7e/v8/Tt6evs8fb39fPy+H59ffv07e7v8fTy8O7w9fT4//3+fHt2bmlpZWRkZWRlZWVpam1ta2lqbG9ubm1saWpucHd8fXt6fvz/+vl+d3V2dXR3fHx4eH3/9O/x8PDz9PLz9Pb7e3l+/vj19/jz7uzp6Ojp6ers6+ns7fDx9/76+P399vT2+/v29PDy9PLy8/Z+dnVzdHNxcXJvb25tcXd7/n18eXFxdnZwbmplZWhpaGhnY2FhYWVnaWxye/317ero5+Tl6Ojp6u3v9Pn8/v96c29vbnN7/vT0+vz18PD08/b19fb08O7u6+3y8e/v7ezq6+3v8vl9d3BvbWppaGpra2hoaGltcHJ0c3N2e/jz9fT2+fr28/n7/P38+/v9fndzdHV1env89/n39vb18/P09vjx7/Dx9fj5+/15dnZvbnFzdnx+/f59ffz7+/r+/f78+Pt9fXt1b2xramxsbGtsbnR9fXt7eX3+/Pj1+P58/f37+fn7/P19eXZ4//r49/r59e/u7+3t7uzt7ezq6uzt6+rt7vP19PX9eHFwbmxra2xtbm5tb29ycnN4d29tbG92dXV0cnR7/vv3+Pl+evz29fj6+3t2evz6fnx4cnJ7/nt9fvz38u3s6+vt7+/u7u3w9fz78vXz9/38/Pz39/n5+Pn5+PLu9HhubW9wbWtubm5tbHJ2fPv+fH5+/X7/+fb19Pb59fb4+/x9enl9fnh3dnl+/Hx2cW1qamxtbW9saGNiaGxvbmxtc//38/Hv8O7t7Ovu8Pf8/v7+fnp4dXN6/Pfw7O3t7evn5+fp6+zs7e7v8vT9+/T5fHhydHNwbWpsbW1ucnv/+PDv6+ns7O3v8e7s7+70+v16eHV0c3BtbnBwcXBtbW9tb3h4c3Fvb3h8eXZ0dnV2fvv18fl7d3t6eXd0b3B3ffnz8vl+d3d1dnd+/f317+3u8/n9fX58ff38+vx4cnFzcXJ1c3N1e/z07u3u6ujo5+bm6e3y9/b6e3p8/v7++vX0+Pj5/P5+/P97enp2dHRvbnZ5d3d2eXh8+/x+fv/8ff3+fXx6fH19fHl2dnV4fXt2/vx7dnR1cXBubmtqbG1wcm9vcXJ5fn5+d3p6eP/18fHw7/Dt7O7w8/bx7erp6Ofq6+nn6Ovs7vt2bm1ubW1rbHByeX779PHu7Ozr6uvt9Pj5/H7+fXd1bm1vcHBtbGtsbnV8eXZwbGtqampqaWpuc3v59O7w+fz++ff49PX6+/f08/b4+//+//9+eXl4ef38/Px8enz+//fy7u30+P59fv/+fnt3d3z79vPt7PDy7+7u8/j8fn57/v1+e3Ftbm9vb29ubm9yevv28/L18+/u7u7t9Pn59/Hz9/v6/f98/PX5fn7+/v19eHBvb25wb2xsa2pqaWpsa21ubGxvevn19ff3+n16//x7dXB0cnBwdv5+fXp6/fj58+/v7u3u6+3s7/v9fXh3d3l6eHl6fPv28/bz8fDs7O3v7+7w9vb6fXZ5eXn/+vf19Pb28/X19fX19vXv7+7s7e/z9fTx7/Lz8fP4+/3+fXp7enp6eXl1cG5pZWJgX19gYWVlZ2ttcHd+fXp8fXp4d3RvbnV2d3d0dnp7eXZ1c3BwcHN6eHJ1e/r18Ozu8vTz8vLw9Pr7fn5++/Pw7+7t7ezr6uzu8PDv7+/u8/j+d3d2dXR4enl5//79+/z8+37//X56e379fXr9+ft+/vf39Pj8/H56eHv+9/Pw7/Dv8fLv7vX+e3v9fHt4c21sa21wbm5zeH79/fj49ft6cnBwbW9tb3JwcHn29Pj6+fb1+fx9e3ZvcHR3dnN1d3d5fHx7e3J4fvXx9Pr+/nz+/fr19Pj59/b59/j9eXr++/bx7+/x8vDu7Ovu9Pl+/359enJubmloa21ucHR8+fTu6ujq6uvt7/Dw8/l9fn19fn17c25sa21vb25udHf++Pv+/Pv29vTy/Xlyb3Jvbm5ramtwdnz28fD5+fPy8/l+fnVsZ2ZnZmZmaGpudf3y7Onn5OLh4+Xo6/Dw8fX6e3RubXJ0c3BubW9yefn3+Pb19ff6+fXw8PL2/Pn08ff4+X54d3z39Pb18fHy8/j3+3x2b25wcXNvbG1uc/338/T28/Pu6+nr7fH29fX6fntwbW1xcG5qaWhpbXJ0cHB2fv379vbz8vf5+PX2+Pf4/fz5+Pj7fn59ev37+vf28vXz8O/u7u7w9v94fXlvbW5ubW5vcHZ6//56c3BvcG9ubGloZ2dpbHBubGttb29vbm92ffXu7u7v7Ojk4ubp6+3v7+/u7u7w+Pv7+PP09Pb08vny7u7y+fr6+37/eG5ta2lrbnZ5//r49PDs7O7v8/n8fX53cG5raWdmaGtsbnV+9+/r6urq6enq6+77c21ramtscHFydXx9/fr6+fXx9Pb08fPz+fz+e3RvbGttb3Byd337+fXy7evt7u/x9/17eHJtZ2RkZmltd3t7eHj+/Hx5e3Z4enp8fv78+fT08vL8eXNvb25wcHBubnBzdXv8+n51d/739fX29vT09PHx8u3q6uXg4ubo6urp6Ono5ePh5uvx+np2cWxnZGJiZGVmZ2lsc3p7fHp8fHx9//38/Pv6/Pr09PT6/3p4/vn4+Pr39fDu8PT3+3l1dnZ0dHh1dv33+Pn7+v57e3V0bWpqbHJ4eXZxbm9xbGllZGVmaGpsb3Z6ffrz9/Tw7+zr7e7u7uzt7vD4+/x7eP3+//v07+zn5ebm6Ovr6+zt8fLw9vx9eXJzcnB1e/34+vv4/f5+e3p5eXh3c3BxdG5rZ2NjY2RmbHj28erm5ufq6/D3/XpycG9vcXd6fX7//Pr19Pb29fP2+Pf6+vf4+/t+dnFqaGhscnN3eXj57uvn5eTk5ejq7fL+e3Jvbmxpamlrbm1tbWtqbnNydXh0b3F0cHBvbWxub29vc3728fDt6+ro6erp6ern6Ors7vT7+vT5/v368O719vP29/f5/Ht9fvn09/19fn14d3d2dXNvb3B1fHdzcnN2/vf6/3h0eXpwa2hqbnJ2d3l+/f78/v5+/X53efz28+/v8fTz8O3t7/f9fnp2eXhwbW1vePz18vTz8/Hx9fn6/X16eHNubW1ta2xubW5xc25vdX347+3u7/Hw8/t5eHNubmxoam91eX17e/n27+vq6Ojr6+np6u3w+Xt5fvx8eHh7fX59/v359vHu6+rr6+3t8vTw8PZ+dXV0dndzcnV4fP78+Pr6fHl1cXF0e3V2dnNweXx6enx7eX377+7t8PX09PLz+/r8/Pb0+nZtbHJ1c3h7d3FwbnJ3eHduamlpaGZnaWhoaWxwcXJ0dnhzdXd8/n59+vHt6ujm5ePj5+zv7/H3/nx4dnN0eHt8fvz4+fj39vHv6+zt7vL4+Pt+eXJxbWtsbXN99fPw8PHz9fr5/nl2cG5xdHt7dG5tc3R3fHz7+vby7Ojp6+nm5+Xi4eLp7vL3/354c25pZWltcHRva2pqbXh+/Pr7+vXy7vD5/n7+fXh4enh5/f39/Xl1cm9sa2lqaGdoZWhoaWtscXh4e3t6/fbx9fHw+fr08PX29vz3+Pf28vDx8fDu7e3u7+3u7+/w8PH19vb3+n18eHZ7fn16e314e31+/3369PDu7vDu7Ozv+nx3d3Rwb25samhnZ2ZjYGJna21ubnd7dnF1dHFzd3dyc3p8+fHw7u728u3t8PTz8/Pz8PDt7evt6+vr6Obm5+Xh3+Hl5Ojq6evv/Xx4dnp+fn18fXtzbWhjY2BeXl5fY2JjaGpucHV6eX76+ft8ef7+e3t6eHf8+ff6+vT4+P3+/v76/Pv5+PPv7erq6urs7O7zfnd0bm1qaWhkZGRnaWpsbnv89u7v6+jl6Onq6uvr7/b59e7u7e3t7O7w9fz7/Hh2c3Jwbm9uamhqamxramppampvef308O3r7Ozs7O3x8PDw9Pp4b21sa2tsa2xtb3l89vfz8PT59/399vTv+P17fH799ezj4N7h6fR9dnF99/Pv8PN7a19XUExKSkpMTk9TWl9pfOvf3tza1tDPzszLysvMztHRz83O0Nniemtoa3zt497e4O5uWU1CPDk4OTw/P0BCRk1f49LKx8XDwsHBwcLFx8nLzcvOzMzQ19vSz8rN1HpUR0NFTmTh18/S1+ZmTj42My4tLC8zPEVLTkxPUmPmzcK8uLa3tre5vcLM0NDKxsPFx8nR0dXe8HV7/trb23RSRD8+Q05l5NXY325URT0zMS8uLjA2PUxXZV5gX3jcx723tLCztLe5vb7Fyc3NycXCwsfJzc7U4e5w+ffb3d5pTT87Oj1GWPLX1tnwXEs+NjEuLC0tMDU+SmX74uHe2czCu7WwsLCxs7a4ur3ByMvNy8rJzc3R2+ZmVkxOT2L+el5JPDUyMzhCVuXQzdHsW0U7MS0sLCwuMzpM+9HKycjKyMK7tK+trK2tr7K3ur/Gzc/S0c7MztDR3+X6X1ROUVNjXllJPjczMjQ6RFfm08/Xd04+Ni4sLCwtLzU+V9rKw8PExMC9ubSwr6+vr6+xtbm+w8rO0NLP0dPe3Ot9b1hPSUtLVlVRSD02MS8wNj1M+9DKyM/xTD4zLiwrLC0xOkzpyr+9vL29vLm2sq+ura2trrG2u7/IztPc3Nvd6PLubG5fUUtISEhNS0g+ODEuLjA4QlzXyMPG0mxIOi8sKyorLTI8VNfDu7q5u7u6t7Wyr66ura2usbe9xMzS1uDk5+Dk5+B+7mdYT0tJSU5KRz04Mi8vMzpFXdjLx8vXZEc5MCspKiotMTxL3MK8t7i4ube1sq+urKysq6yusrjAytbo92ZfW2RdZfx09GlVR0VBQ0hHQz03MzEyNT1IYtjMxsjQdks7MC0rKistMDlP6Me9urq5uLi1s7Kwr66ura6wtrzAyM/W5HJrZ2dj6+Xe3XRYS0pJTUtFPjYxLy8yOURR683Fv8DK/Eg3LiopKywvNT5Zybmyr7C0tra2srCwsLCwr66vtLvG1OtoX1hPTE1LVHfp3fZWREA/QkZBOjQvLi82PElYeNLFvr3B30g3LiwpLC0uLzVC7bqwr7G2urq0sa+vs7OwrauqrrfAztjZ61lHQEFGVd/V1ONgSktPTkxBNzAvMDM5PD5FVtvDu73KaEE2MS4sKCkrLTVL2b6yr7Gwr7Gzsra6ubayr6yusLW8v8LNek1APEBMS1Jy8eje7VdRT0dBPjgzMzY3O0JLY8+/urm8zGhHOjAtKCUoKy40RXfDs66vr6+xtLW5vr66t7OwsLS2t7i9yu1PRUNGQj9GU/3i42Rdb+xgSTw1MTM2NTU5Ql/Mvr3AxtD7SjguKCUnKCorM0fPuLGvrquoqayxuLu4tbe6ura1tLS3vsnT6lZMQzs+SFZQTk5a6tnyTEA+Pjs2MC8zPUxg6czAvb/Lc0Q6LyonJSQlLThM3b60raalpaipq62wtry9vb7DxcC9wcnQ2OP3WUhHS09FQUJNYGBRR0lKSD42MzQ4OjxCUuTNzNPe+VM/Mi0oKCopKi5C8cK3rqqloaSoqamssbi/w8PFztfLxMva3t3mdkxJSU9LQD9IVlhZTExUVUs+Ozs9PT1BTe3QzMvKzOFVPjUvKykpKSovO1DQvLCrp6SjpKanqq2yub7GzNjj3ed0ZvF+aFhNVVJVSEJETU5IRkNIS0c+Ozs7Ozo9RVbv2M7LyM7wT0A2Ly4qKCouNDpOzrqwqqelo6OlqKutsri+ytLX3ud4bmhrbWdcTVxgXU5KTVNWTUtISk1JQz4+Pj09P0lZ+d7V0M/eYkg8My0rKSgoLDE6TNa9s6unpaSjpKeqrbK5vsnc7/x4aGJl9+zn8Gju8PhZTU1UVExJRUdJRkE9Pj4+PkJPZN7SzczO219GOzEuKygnKS4zPE7OvLCqqKelpKeprK+1vMLQ3t/q9e/lfHPn6PNfWmFcW01HRk1RTEdGTExKRD49P0BCR1J32tHQ0tjrVkM2MS8sKiouMztL5MW1rKuppqWnqqywtrrAz93d6Ovg3Ojt4ODp6l9YW2FVR0JETFBNSEhNT0xBPT1AREZIUv3SycrP2/VTQTkxLSssLS83QmbHt6+sqKWlp6isr7O6xdLb6vf48OTe3uLk7/xeU1ROTEVCQkdOTktISktLQz07Oz0+QkpY7tfNz91qU0Q5My4sKy0xN0R5yLmuq6mnpqiqrK+2u8DJ0dbZ1dLOzNHV3ultX1FKTExOSEdES1hgWlFPTU1IPjo5Oz9GTl3mz8jJ1PpYRTw0LisrLTE6R3TJt66qqKenqKmtsrrAyNHc7Pns3NTV2eD0b2FZS0VHR0dFQkFIVV1VTkxJSUdAOzo8QUpXbuPRycrU/VNEOTMvLSwuMztN38W5r6qop6iqrK2wtr3Fys7R1dna1dPX5W5cVlVLSUhIR0dHSE5bXVlTTUlGQj06OjxASll9287Jy9d7T0A3Mi8tLS83QV3Sv7atqqmpqautr7O6wMbKz9LX2NPO0tvpbV5ZV0tJSktKSUpJUF5iV09LSEZBPDg3Oj5HUmbq0szP4GRMQDkyMC4uLzdAWs69ta6qqKipqqyusrnAyc3V3eXv6+Tk+WxnY2hbWlpbWFRRT1RZXVVOTEpIQz46OjxARk1d+9vU2OxeTkI6MjAuLi40PUvryLuzraqqqqqsrrG3vcTIzNPX2NnW0tbk8nx9W1dXUk9NTk1VWlpTT1FOS0RAPDs9QEZOZefX0dTlYk9DODEvLSwtMjlFbMq6r6upqKioqq2xuL3Cytjl6OHk6vV+c/1kU1VUU0tJRktVWVJNT1JSTUZAPkBBQ0hSdt3V1tvqZkw9NDAuLCstMjpJ8Mm7r6qoqKipq6yvtry/x87V19fT1dfe3/xqcVxTSkpITldWUFRgXlxTTUhHRkJDSlpx6N7i62xUQzsxLi0rKi01PE3gw7mvq6qqqquvsba9w8bN3dvU0tjP0dHa4+RhW05MR0xTU1JUWlZZUEtFRUZDRU1bbOTd5vVeTT01MC0qKCstMDhIdMS2r66rqKqsr7O4u77HysfBxMXDwr/KzdPqVklEQEdOUUxi+vhwZ1hRV09MTmfw39ve62hRQjUuLSkmJiktNT5T1Lmvra2sq6ywt72/wMbMzMW+vb++vsfCzP1OS0NAR0tITGhjampeTk1NSUhPXmzm3+T/Xkk8My4sKScoLDE7SfbGtrCvr6+usLe9v7++vr68trS1t7e/wcLbTkhCPEdTT1Hx83R+W0hFRD5BSVrr0M7Q2HRLOzIsKSclJysxPl3Vv7Wwr7CzuLq+xsnDwby4t7OxtLm4xNLOdEdFRz5Ne+7fy9l0dU0+Ozo4QE1k07++v8psRDgtKCUkJCctN07Rvbi0tLa5vcfQ2NHKwbq1sa+ur7O7v81q7HxPSlFLYc/N0M3aX1RCODQ1Nz9T6Me7ur7KXjwwKyYkIiUqM0brzL61s7W5vsbCwcLBvLeyr6+wsrS5wtTcfffoaVdodmnk3eTp6lRHPzo3ODk+TXDTxcPM3FU7LywoJSUnKjJBV9nGv726u7y+vr25trOysbCwsLO3vcHGyNrg3ed1/m1ib3pnWFlORz47NzY3PEJNa9/Y2VxLQjUtKyopLC4wOEty0Ma/u7a1t7i6uLi4ubi2s7Gztri4urzH1Nvh+GdeWmf87vD1a1hNRD47Ojo9P0lSVE5PS0ZAOjYzNDQ2NjxGXOjSysK9vLu8vLy6urm3tra2tbW2uLq+xs3U4fh4ZV5fWlNSTUtKR0ZCPjw8PTw7PD1ARkpMTlFXWFVUUlNVVl5udurWzs3JxsHAv769vb28vb29vsDBxcnN0Njj9W1fVlBNSkdGRUZHQ0JERUREREVGSElKTU5RWl9dYWz86uPf29XS0tLPzczNzMzMy8nOzsvO1tPS19jZ6unj7+jz+2RqbWRUUVNaVk5UVltaXFtqY11bW2ZkXF1wb3Rne9faev3f6dzi7vrf6dvU28/Q4HTWz+j43P7u3/nrcmFeePD4aVdYbntkZlJPYfxoXVVXXGFocvVuZmnw5u5kWGtxcGdtaXbq3eDh3Nva3dnf9HVz8/BgYvVwX2338fvpeXni8F97Z198e3t8Z3fb4u7y8OPn5+/w7fhx6e97fvr79m5uempwcGt93udp/Nvc5On16+ni6+/f6fHg3Oz0dP91aFtcWl9nW151fm7x7P59e25vdHh8bG19//Pl3+pmfd3l82xo8HtkZ15t+m5Xbuh2X2z6dm1sem32+PF48Obq/u1xYWBweHNqc/rw5+fs9vl67PRvZvr49mlse/Xv+fz55OTk3+Tq3uZy9etxaPlvZGT17Gdje/T07/f8eOXkfvvu73F2921jcH778vzv+HPq6H5tbm1tbWxkbmJeZ3ZrbmFeenhoe/Fsdu3o+d/g8ffh4O/9cuTubu7wV2na5XFe4tjU4X30z+FmW/bzXE9aW15XVWR9ffZs+fL31eJncO/t4mpi/X5qZm705+5lb9vkatzhavLc+Gzd7GHi31bnznFq2HFn8d9tY2xqdtxvYu/m3HBv7/dxe2dvb2b7+G31d/j0//ne7O7v6O3q7Wlz4PFWZWVqX2ZdX2VgXH3ubnrta/DvfW5rZWp3ZGn0Zmnubl15fXR+9/L+3994deDu+Hzs7W3y39306Ofb4Onk6+3j4mh16+tm/mdt7PRsd25r8X5w/HJra/79cGRv9fLt8u379f3ffW1seG11d33tfejl4/fs/uj6bXz7bWd9ZWV1fWVsZGdw9fprZWVte2lkYF1iZ2pfZF5hY2xxeXv98+7w7+js6/R47vXw5+775+Pw7Ozo6Ojq6+zt8Pjy6ODr6+jj4uHn7u339fpuZHV5bGVnaWdhYmdnX2FsbW1wc3BzfPDv7/Pr6vT17ezw/HP69/Lu7vTv7e7z9vLx8fDu9fTy+n15d2xoZ2hnZ2dobGxsbHV8ef7t6uzt7+3w9vv8+v329/758fDw9P339fV+cWlpbmtsa2NjaWhnaWlnbXV1cG54/P56dXJ0efz07+nl5N/c3d3d4OLh5+vm5+ru8PT9/Ph+dXJyc3X9/25mYl9hYl9eX2FhYmNnZmhsb3J0en317e33dXBqZmdsbX3/cW30fmt3ysvcXVRjxbe4x1Y+OkBR4NXJv7WurLHFTTQuLS8wNTY5PkpRW1VLR0le3cfAu7i0srK1ub3CyMzKzMzO0dfeRknfd0tAPUu9srLNPi0rLTVBSWfFsqypsdA7LSgpKiwuMDdATE9MQENJVWvRwLWuq6yxuL2+vbi2tbWzsrCxucn3UEpNT09OUF5fSVLtSzYuLTjnxs9KNS8zOkFGRmLDsa2uvWQ8My8uLzA1PEZPTUZBPUhWe+jMvrGrq665vr+5uLe6urexr7K5xtxxaFZRT1dbYlxURjpGXUk3LzJJxb3VPjAvOT9JSk/cua6utcxTPzw3Mi4wN0BLST46PEdYYOPQwbuzsK6wtbu9vby8vLu4srCwuMDU52RUR0JFTVxcTUE9NjlISjszN0nKvtBGNTM7Pz49QW69sK+1v85wTTwzLzE5RExKQj5BSlZRWOrJvbeysK+xtbq7urm7vLq2s7O3vsfS7FZHQ0dMVFRLQj48ODQ7SEI5OD9lztdPPDc7Pz8+RFbPvLe3usDM7U49NzU4PUFDQ0BDS1JWV1vhxLu3tLOwr7C0uLu7u7u7urm4ub3H0vdaTklGRURGR0ZDPz07OT1GSEA/QUped15ORUZJSUlIRk520ca/vr/CydZpTEQ+PDo6OjtAS1lt69DJxr64tbOytLS0tri7v8LDwcDEyMzS3fddUExJSEVCQEBAQEBAPz9ESENARElPWlhTVFZbXlxbXFxt5tjNx8PCw8jO3m1XTEQ/PDw9QUZNWfbazL+8vLm2trW1tre5ury/wMLHycnQ2+V0YFdNSUVBQD89PT08PDw8PkE/P0NHTVVVVVleZmleW1pZYv7m1cvHxMTIzdbqZVFHQDw7PT9DSE5bbdnGwb+7uLi2tri5ubm6vL29wMPGzNPX5HpjVU5LSEVEQUA/Pz9BQkVHTUxISU5WYGdoanB6fm1lYl5bYG/w2c3JycvP2ONwV0pCPj09PkFHTll23s/Evr26uLi2tre4ubq8vcDCxsrN1N7l/F5VTUhFQ0A/Pz8/Pj9AQkNESExKSExRWGdtZ2lrY2FeXFtZWV5v5tLLycrM0Njhc1tPSURDQ0ZLUVxy5dLKxb66urq3t7e3uLq7vb7BxsrO2ux0XlhVTklGQ0FBQD8/P0BCQkRHSUlLTFFZWFZfZmzx6PD0fWplX1taWVpn8drLxsbHyc7W425YTUdEQkNGSk5YcuDPyMK+vLq5ubm6u7y+wMLGyc3R19vk+GxgWFFNTEtKSUdGRUVERUZGR0dHSElJTldTUVpaXHF+c31yYmRiXV5fXWRz6NXMysjKztXe+2BUTEhFREdNU1/+49XJw768u7u6uru7vL/BxMfKzdLX4/hyZVxYVE9NTEpJR0VFRkVFRURFR0lLTE9WWFVbYWT/5unp6PhxcmpiYWBibPjcz8vIyMvP1eJyW09JRkVGR0tQWWjv2tDLxcK/vr29vb2+wcHFyMfL09nh+2xhWVZUUFFSUVVUUE9NSUdGQ0JDQ0NFR0lMV2Jqcvz76d3c3Nvk83luZ2ZfXmZ+49fQzczN0dnkeF9UTElHR0hLUFts7dzPycTAvr6+vLy9vr/BwcPHys3V3eh6amddWllXV1hVUk9LSEdFQ0FAP0BAQ0ZJS09YW2BtcPjo6ezo7n1tZF1bWVpdZ/ng18/MzM7Q1uD1aFpVUE5RVVljeevaz8rGwsC/vr6+vsDDxMbJys3R1+F+aVxTUE1LTExMTU1LSUZDQkE/QEBBQ0VHTE9UYW958+3t4t7e3Nvk6+30+nhtdPbq3NPOysnKzNDb7HJcUU1KSUpNU1xp7trOycTCwL+/v7/Bw8XHyszP0tfe6PZuYltXVFJRUFFQT05OTU1MTEtLS0tLTE1OU1hZWl9pcHf38PL5fnNrZWNkY2d75dzV0M/R1t3tb19XUk5NT1NZYHbl18/LxcC/v7+/wMPExcrO1Nne5u3+bWhlXVhVUE5NTEtLS0pJS0tLTVFSUVRXWFtfZmdnbW1xev3+dG5raWhnaXH+6dzVz83Mzc7P1uDzb2BWUVFUV1tn+eTZz8rGw8DAwMHBwsPFyMvO0tjd5vxmXFdRTk5NTExNTEtLTExMS0tKSUlJSUlJS01QVVtjcH77+v95cG1scH3u5dzW0c/Pz9HU2N3l7vp1bnFsde/p5dvSzs3KycjIyMjJy8zNztDV297m+21gWlZTUE9OTU1NTU1OTk5NTU5NTk1MS0xNT1JXW19ocv3w6ODe3NrY19XU1dXW19na3N3g4+Li4N3Z19TQz87Nzc3Oz9DQ1tnZ293e5Or2eG5pZF9dWldTUE9OTU1LSUhISEhJSkxNUFRYXF5kaW5zc3v68uzo4d7d3NrZ2tjX1tXV0dHQz8/Pz9DS09PU19ja3d/j6Ovx/P92bGdjXVhVVVNRT09OTk5OTk9QUFFTVlpbX2NnbXn58Orq6ejo6ufm5+Xm5ubk5OTh3tvZ2NfV1dTT0tPV19fR3tTd0+nb49zl+37vXe5dXlxVYmJYXVtSfFRhUVRWWVFYTlpZWl1YYGplZ21t7nj08Ozl5e3m6enp7+/u8vDy9/by7+/t5ufk4+Th5enm5uPl6urp5+fq7PD1/H35/fv8+312c25samlnZ2BgYWJmamxsbW56fXt5+/r08uvp5+jm4t/e3dzf4OTo7vP4eG1oZWlpb3RxcnV2cW5tbnBwd3Z89vb49PH49/d3bnBxcnV0bWtsbmxtbnF3fPny7u/4/3h4enJwcnV2eHZ2cm9tbGtpbn3u6+zt6+rs7fJ9bm1sbW1ra2xubW5ub25wbnB1fvXy7+3v7/P07/T8dm5vdHZzef767+ro5uXk5ujm6Orr7vDx8e7s6Obn6+7t7ejo6Ovu8Pb+/H5+fX18e339/f97dndycnRzcG5sbGxrbW5ub29ta21vc3RwbmxucHN6eHBsbHBrbW5uamdtfF/u4vxWX3TWz9xhT1Zo3tPT19PQz9f7VEU/P0RRdNzOyMK/vsDJ2HNWTUtJSUlNU2Xu2tLNzc/T2uT4alxUUU9QVFxhdPrq5ebp/m9hXVpbXWZu+/Dr5uPg3+Pn5+fueW5uam168e7p6O/r6+v0+XV2efv89P1yb2tnZmhkZWx5+/Hq6eTf4efm5+zv7fZ+/PT19/Py6un0cGtmbPz1dXBvbn7r5+vx/Xf66uv7c2xucHH9/XVvdH7s5On4bmZmcff2d2lfXmJnaGNgXF1fZGxvaF1cX2h29/Z4amhx9+7q7npzd/bs5ef2dnJ3/eni5+3y7ebf29rd5unj4Nza2t7k3tvW1Nbd5uzx+n1wbnFxe/L6bWNYUVBUV1teXWBpbXh3Y1hQTElKTVFVWVxm8+TZ1dPQzcnIx8jLzcbCxsrU3tjNyszaWkQ8OT1JZdfJxsbI0PBOPDEsKSkrLzc+RVjz2s3JycvIycfCvrq2s7GwsLGzt7vAzNnvaVxXVFNVV1xeW1RQU1JKQz4+Qk9ke19IOzQyNT1W0r+6t7m9ye9NOzIuLS8yO0Bb4drO08zLxsPAv725trGurayusLa8wMbL1N90ZF5ZVk5LS0dKTlZRST88Pkdb8fRdQzcxLzQ+W86+ubm8yOxLOzQvLi4xNz1HU+nd0tTb1tXMx8C8uLazsbCvsLK1vL/GzdLe6XFkW1hUVllXXF5jV0tDQENOX/H7VkA3MTE2Qn3GurW2vMtjQjcwLy8wNDg+RlBh7trOysjGxsK+urezsbCxs7e6vsPIzdLY3eb3ZVdPTExKTVNfXE5HQEJJWXJ1XUg7NDAyOk7Yvre0tr/XTzw1MTI0ODw/R0xZaN7OycXGxsTBvbm0sa+wsrW5vMDDx8rLz9Pd625aU09OUFZiW01DPDs8Q0pNST84Mi8xOEb3w7m0tLvMWT40Ly8xNDc8PkNIVXDYysXCwL+8ubSwrq6ur7G1ub3BxMjKzM/Y5mlZTk5NTlRcX1FIQEBCSlFVTkQ7NTIzOENqy7y4t73PXkE5NDMzNTg6PT5GT2rZzcfEwL25tLGvrq6vsra6vsTIycvN0djrY1ZMSElLTVlmalRKQT5ASE9TS0I7NjQ2O0hxzb+7ur7LcUg7NjMzNTY4Oz5FUXjZzMfGw8C8t7Owr6+ws7a5vL6/wcLDxcrQ5GRWUVVZWWVhbVNKQj9CR01MQzw2MzI2PUz1zsK+vsLOfk1AOjY2NTU2ODs/Slv63NjQyL22sa6trq6wsra4u73Aw8TEx8vU4HNdVVdTU1paXExEPj5DSExMQTs1MzQ4Pklb7dfNyczXdk9BPDc2Njc5PEJKU2b95tXJwLq1sa+urq6wsra6vL2+wMHCx83Z7GVZWlpSVlRWT0dCP0FISkdAOzc2NztASFNj6tHKydD0UkQ8ODc3Nzg7P0ZOXf3dzsfAvLeyr66ur7K1uLq9v8LFyMnKztng7vHxbV5dUkxHPz4+QUNCPTo3Njc5PD5ETmnby8rO4WpNQz88Ojg4Oj1CTFr92s/IwLu2srCvsLGys7W4vL/DxcbHyM3S2ePr9mxka2FeU0tIR0dIRkE+PDs7Ojo7PEJOZePZ2tfqZFZJQT07Ojs9QklPZuzUxr66trOwr6+vsLO0t7q8v8PEyMrQ297qe2ZYVVdQTUpDQT8+Pz49PD09PD89OTpATFNYYV5x29nrblFJSEdHRT8/QkdTc+HPxL23s7GvsLGytbe5ury/w8jLzdLc6vtpYWZdV1ZPSklHRkZEQ0E+Pj08Ozs6PD9FTVls+Ovs/mhaUkxIRkVGSlBf9dXJv7u4tbW0tLW2uLm8vb7CxMXHysvP2N7pal1bVE1JRkNBQURDQUJBPj08Ozs8PD1ARkxYb/Pt8fZyX1ROS0tMUFhm7dfMxb+9ure3t7e4uru8vr/AwsXGyczN0trm+mxeV09MSUVBPz48PDs7Ozs8PDw+QENGS09UW19ja3J+dnBraWxse/vt3tbPycbBvry7u7q6urq8vb2+wcXJzc7W3e57YF5UTkxKR0VDQUJBPz8/Pj8/Pj4/QEJER0lOUFdbZm73697c2NPQz87My8jIyMXGxcTDxMPExsfHyMvMz9LV39zn8W9iXllRT1FOUUtNSktLTU1NS01OTVFOVlNeVlxbW2BUaFJpWm9g6Xbn6+Xb29XdztzM3tDXz9bQ29jX3+Pp3vrg5/j07uzm9Oh4ff5zd/z3aWpmemP0bmt1+V75YOxbamtcYV/9XfRn+m5v+mvqfejp5Ofs4uLd6vNm4mDlVONW01Pccuhr2+hw93DnZOZc3lrUamdi2VprbmB29u5e3V7XXeRf0VPnamtnfVhfe3Ri7Wbc5evy++576Vlqaf/tbP112V3tWOv/bF1p/lzfXHtd5mXtWGl4+XfxZufa4eHt5+7gaeF83XnrYut75Xvr/db78u7h7nxxZ99ifm7sbNpmdl75XHdbamfoe/Pr6dne2eTaeOV533b3ZOft6/9ubW98Ym3+7W3x/Olwe2Dt6/9nXmh6dVlpZG5oZmV1fWReYXx1/Gd27+t2e/rk3+tybWthbV9jaen+7+fh39zwYm9lcnh1YPXm5+3l9eDl82xdZ3htW2936efY4OTe/Gn8+2fv+Pjr6Hzn5nnx9W58fm56a27t7mrz5eTe6G1t5ed9X2Jw6Pxib+7j711eeerxZV936/JkXP7o6mpbZuLsdV9t++rraW575fDseN/Y5nj8++nj/3H+6en/Xmrw3Xtta3Xy6HdmefLp8nVZaHz3/mFbZl1ffXf54uv4/PXg5Xdu/n38a27q/vBtVVNwa2RlW1xncfrv7N3p/Onh5d/i+eHb2NbX4enqfXpiafXf4+br6ePs+F9kdHF18XJdXlxWUltq/PTr7Hx98P1oZnvy8XtrXW7i4Or+7eTf6nRp+9za7Wxu6uD+d/3w5+Hvb2756/dtZXDq5PdqbXl7cFxcdf76/V5ZaHrr7G5kb/Ph43lgZW10bF5gbnh8d2/56OHwePLs7Ox8bPXo6u/28n1pX15eb35kWl1r+PN0aHHt3NvncXTo4+DramR+7H1kX1pcZ31zdH7w8e7q7/j17ebd2trd4O34dXH97vN3bWx37uvl3d3c2dzc1tPZ5et76dzz4+dXbeZpbvRXX2xbXG1jYXBmZGtoaHNvbfd6bG1mZW9qYmBhY2lrZ2hfXmZoa3t9cfXq8PH0e/v1+/jx/ffo6+vp6+/o6/P16e7u497d2dvh+XBqc/19bmReWVxaYWVqZFxYVVxmcvnp6d7a2dzpaWFiZW1fXF1dYP3s6unj28/Jx8nMy8jJy8zP0M/DyuJSTlbOv8R5PDAuMz5b8NfNwsHD1005MTE3P0ZNQD9ESktSV2DSysTJx8e8uba2ub29u7y7wMXP1N/c7mtUSkRGTlzxe2VcRUVgakxDOjxkyMHYQC0qLDldz8bBwb+8wNdKODM3P1NrYU5JSVda19PX4dXMvLKxsru/wbu6tbi6vby+wMfV6lxSSktJTUxNS01NTUtKQT1EWFpOQDxCeMvIdT0vLTJD783ExMC8ubzNTjgxMzxPdWtYSEVKXejR0t/bzr62sbW5wMPAu7i4vMDDxMDAxtxdRT8/RE1aXVlXT1NPTEg+OkZpcGdHPUFuz8foQTEtLz/hwbm5ury6vspbOzEvNURfemZMRkda6tTMzM3Nwbu0srO7w8vIv7u5vcLIxsbG0XVLPz1ASFJfWlJKSEZEQ0Q+PlJzbFVGPkr7zcx7PzEtLz79wri2uLi6v9JOOTAwN0h72+xUSkdc2snIzNbRwbmxsLS8wsW/u7e4vMDGxcbI2V9FPTs+R1BcXlhOSkhHRUE7Pk5ZWUw/Pkx7ztBePTEtMUHuv7e1t7e7wd1HNi4tMj5T+vRcT1BV4s/Mzc/OwLexrrG4v8PBu7i4u7/GxsXHznNKPj0/TWP0+WFRTU9OUEs+RFNcXVZGSFzqz9hVOy8sLzxjw7e1tbe8x3tCNC0sLzhHZvNuX19w1dbR0NfUyL23r6+xub/KycrEwMLCwsLGy+1VQTw9RE5s63FdT0lHSkdHPD9MWVtYSEVb6srL6kU3LzI9W8e5tbW0ub3NWzswKywxPE5x6ebf2s/P0d9+8NrHu7a1tLm9wcbLy8vLx8XBwcPO4lpMRkdLVWJrZ1xTTktKS0lKPklUZltZRUda58rL3ko7MjY9WM+9uba2ub3SUzkuKiwwPE/64NjX08vNzdXs59jLwLu7ubq8vr7CxsvRzczHxsbQ3mZWUE9OUE5LTEtLS0pIREY+QVJgYFhLRFrox8fUUD0yMjlH3cC4tbK1uMPtQTQtLjI8TnHb1dDSzMzO2/xl99LFvLu6u7u9vb/Bx83V1dPSz9fkY1RMTExQUU5OTUxMTEhGREdJQFBgcWBjS1rpz8bObUM6MzpBacu8ubS0t7zOVzwyLjA3RWPf29rg3drl6WdlYt3Mvbq5vL/DxMDAv8fO29nUysvO5WBPTU5QWFFQSkpGSEVFQkJES1RUX2llVVBMVfjVzttbQzs3PUrsxrq1srO3v9xLOzQyNz5ObuLY19vc3+r4enzr08u8ubm7vcTAv7/AyM/Y2tXJzc7gcFRTT1VYU05JR0dMTE9KSEZHSlJUVFJPWFts8OL3bVxZX3Ls4uV0X1VVWV5mdnv66OHl82lWTEpMUFx66Ojn6OTe3trY2dXOysfGysPK3L68vMjjSfbLt7O+ajksKzZHybq0tbO3ucpPMygkJS06XXx1TkVASk5ZV0xPX9K+t7m7x9bZzsa/v8PGzMnGx87jVUhGS2Hg0tPeb1tQTUtJR0lLUmN4/XNhV1NTXGJkZGFdX2t97+vu8e/p39zZ2dvd3t3Z1tfZ2djX2Nfi3uTr/f117ePo8GxjXWN13tzd72tbWFVSUVFWW2z55+76Y11dX2V1//7z8efj4OXr/Xl1fPTu7PP+/vv/eWxoYGNjbHFxamFfX19fY15eX2dt/Pbr5ubr8/Do397c39/f3dzb2tnd4uTi4OLi5OPk4Of2dW9vbW1oZmtuaWpoY11cX19fW1lUU1RbZ33w7e3n5+ns9XVobXT57ufm6ezp4NnU0tjd4lXx0M3WekpQ5MW6wuxAMy86Tc+9uby9wcjXTzsvLC04SOzZ7llJR09n6dvq7t7Nv7q6vsfR0crBvLu+wMbFysrL5lJFQklx39PxVUVCQUpPTk5KS0xSUlFGQD5CTGL/+WdWVVhhbPt19/Le083MzdTc3d7Tz9HW2+Dc19bZ6WpcXm3s49/g7/v+/fTt6+rr5t/d3+b2c3N67ePg4uTr6vD39Ozp5uPk5ujxeG9xen7t7vb3dPtqXVBOTVpt/m1XS0VDSFNf+/XzcmpZUklCPjw/SFdk/mpqa+7c2dfY2tfLxb68u7/CyMvQ2N3p6efa3NTj4WRKdvPgeG5Ke9vCwNdKNi0sN0jMvLW3uL/D3U06Ly0vO03Y0NDtW1Nde+bZ6N3Txbu2t7m/yMrIwb29v8LD0MPGz35RRERNVHRZVEZDPkVFS0pGREJHSExERD4/RVBdaV5VVVFfav349Wv15NvR09vU193a3+ne2dXX429USkpPXuPOyMTFzNj8T0hGR09d7NfN09Po8nzu3tDKyMTFwcPCxsrX4Orl3NnZ2t/q9PBbT3BiZk9LRF7gyMjmSjYuLTM9dcu+vbzAyehJOC4rKy83R17y4/z79OPd3eHcz8a7t7Oztbq8wcK/wsPLztPLzs7jXk1DRUxi8NnifVZMRURBQEFBSE5aYWJXU1NRWV5ocXNnbfzp2Nre09fU2Nzs3NvV1/BgTUZFTmHWxL28vcHJ1WxOQTs5Oj5JXe3f0dXT29ri2trZ09LKyMLCwcPEx8rO19/t/GphWVZVVVZYWVpaWVlZVE5NQ0pNVFdWT1Nkd997WkU6NTU6Ql/Ww725ubvD2lQ+NzExNDpEV+XNwLy5ur3Ayc3Pysa/vLy2tra5vMLFydLXbVpNSEdNV3Lj4+H4blpNQz06ODo9QkxWX2psb337ZmReWVZTVFhq8dzZ3O1gUktJTFZw2szFwsLEytZ4U0Y9Ozk6Pkhd3szCvby8vb/Cxs3Kzs7OzcvHwb++wcTM2e5pW1JTU1ZYXGFdW1VORkI/Pj4/QUZKT1htc/rq5+n3bV9fXF1dXVtTUE1OUF373M/Lx8jKzdbjdF5PSkVCQ0ZNXOvPxb+8urq6u7y+xMfKztHU1dbQzszN0dr3X1FKR0dHSEtMTk1MS0lGQkE/P0BCR0xRW2f+5ujd3N/j6PF+/P71+Pn9cGllZ2334dTMyMbHys3V4HVbTklFQ0NFTV3r0se/vbu6uru8vsHFy8/Z3uPp6Ofk39ze5vVuXlVOS0hGRURDREVGSEpLTU5RVFNSUlNVV11eZXB5/fju7+32+nBtaF1YU1RWX/XbzsjDwMHDyM3Y8GFSTElJSlBd/tnMxb+9u7y8vsDFys3T2N7l7fj7fHx8+fhxZl1VTUlIRkNBQkFESExPU1lcYmZmYl5cWFRWWFlcYWhpePbt6+3zb2VfXFxjdunZzsnGxcjM1eVuWlJOTE5SXf/bzMW/vry7vL3Bxs3R2N7n9v5saGpvfPLs7vlsW1BJRUA/Pj4+P0JGTFFYYWdscGtjYVtZWlpfZXL77enp5+nt9vb4cGVfX15n/+fYzsfEwsPGzNTgd11VTkxNUVlu4tHKxcG/vr6/wMTGy8/Y3un+bWdjXlpWUU5MSkhHR0hJSktNUVdbXV9fXVtcWlhXV1dVVVVXWFtdXF1cXmFka3f06eHd2NTR0M3Mzc3O0tXX2dra2tra2tva1tTT1NPS0tDPzs7P0NLX3OPr7npsYFlWU1FPTk5NTExOTk9QT05OTk5NTUxNTU5PT1BRUlNVWV5lbXj16OPe29jW1dTU1dLQz9DR0dLS0M/Q0NHR0dHR0tTV2dzd4Obm5OLo7Pd0bW1sZV1aWVlZVFJSU1dbXlxaWVdWVldYV1hZW15kamxub21qcHd4evjs7O7u8O7t7Ozu8u7q5OHi4uTi393d3t7e3d7c2tra2+Lq7PH4fXlvbGtqaWpvcG1wdXZubHBvcXNva2NeXFtcXV1dW1xjZ2dnaWtuamtscHz77+rn5ePm6Ovv9vjw9P15eXlz/vTt5+Lf4ujt8Pby6eTh4ebl4uHj4Oju8vt+d3r4+XNtbXb06+Xq7fH1/XdzbGZgXV5kanP09fr59Pj2/W5lYF9dYGVpZl9dXGFpbnJ2dnj+/+/p5+jo7vPv+XlzdXj9+vr28erm5urs7/tydvv7/3398+zn6+32fm1jZmdrbG998Oro7vn59n11em9paGViYWlzendsaWtzdXFrY2BjbvHo5ebm6efg29nd6fp6fO3l5Ojm4+Xj3Nnd5n5qaG17fntxbmtyfPfx+Pl9dvzu6u37cGxsfvDn5ODh5+bw9vn9c21nY2Zna2tzb2NiY2VobGhoamtuamprcHp9fXR2ffDu+HlycP78dnRxeXFua3BtcHNwc3Bram54+/Tv7vby8/x8c3359fb3fvfw7O7u7u7v8+zq6Ofh4+bk5+ju9np3c251b3R0ff39+nxya2tpcf3u7Ozs7vPt6Ojn7v//8vDt7O3v7/N3bmhqanV4cnj28/ny9/5+/3BtbGhkXl5hZWhpamxxbnVudG9z//3z8uvt5+bm6Ojue3BqcHR+fP32+vh9d2ZfXFtcXWNpbW5wb3F2dv15enn57Obk6ez38Orh397e4OXl5ebj6vZuaGZpcvzv9vb17urp7f1rY19hbf38/fv/+f788fD4d3Zz/e7o6fB+c3BubW5vbm1oZ2xvdnt4dG5ubXj66+rk4d7d3Nzf4+jq8/x3enz68/Ly/H1xamJhZGZuePbt6vH+cGtnZ21nbWxraGhrbXp+9vz++fPr6OLg4unx/nNoZmZkZWlwcXv9ff93cHBta2xzfH39/nZua2ZnaWdmZ2dqa25wc3p+8O7r8PT4+vb4+nt2bm5wd/z16OTj5OXn6ujp5+fl6/Dx7+jg29zb3t7f6e739PHt6OPh4eXm5uzv+f15eXNtamRfX19janBvaGdmZmRmZGNjYmNmamxudXZwc3ZubGhmaW1yfPPu8fj7fn5+fnpzcnF2c/728vb2+Xl4fX7/fHlzaWpscHn4+e/t7fD5/Pt3c3Rvdnr06+nm4+jze3Nva2xtcn3z5+Lf3t/f4OHi4eTo6+zv9u7q6erwfXBwb2dye/Z2bGBeZXL0/WxZTklHS1f+2MvJyMvR4GtUSEE+PkBGT2Ll0cnGxMTHyc3P09HRzsvLyMrJy83T2u5pWU5NS0tNT1JVVllYVVJMR0NAQUNJUWD65uTl7nhlXVpYVFNbYG519ufXzMXCxMvdZU9MT17gyr+7uLm8xNB4TkI9Ojs+Rlfvz8bAv7/CyMvR0dXT0c7LycbHyMvQ3nVeUk5MTEtKSEdKTFBWW1lUT0tGQ0NCREZKTE5WWVhqfeXq+nJt8tvPzc7oXUtERUto0sC5tbW3vcroT0A6NzY4PUZW58vBvLy+wszS2djTzMbAvLu4ubm8wMnZ9GBYU1RSV1laXl5oYF5YT0pGQD8+PkBCREVGR0lLT1NWW1hUVFFWWVxjXPXr2tzb3NnRzcvP0O1jTkhLUXnSwry4uLm+y+ZSRT06OTxATnnPwr25ubu+xcvYz9XLy8bAv7u9vMLE0Nx1W1JKTEpMTVBQU1JOS0M/PDs7Oz5AR05XUFpaXV1UV1Bce9rPzNd2UENBQ07zy723tba7xdtRQDkzMjI2PEVb28m+u7u7vcDExsXCv727ube1t7a7vsfX6FpaTk9LTUhJSUpVU19dWE5IQT49PD0+QUNFSUtQW2hm+evb3e/2Z/nezMbEyt5aRUFATfTIurOvsLS8y2RGOzY0Mzc7RFjfysG+vr/EyMvNy8nGw7++vL26u73AzdZ4a1dTSUhGRktOX2JwYVVLRT89PDs9PUBCRUlISkpQUFRdcux2cF5g/tvOy8rVeE5DPkBMfsm7s6+vtbzNYkY7NjQ0NztEVuvPw7++v8LFyMfFwL68uLi2ubi8vL/DytfpWlRMTk5VXGRwd3xgWUtEPjw8PD5BRkhLS0pKSktOTVBcbPZw/m354NHNy9LyUkM/PUZe07+3srO3v9BbQzo0MzM3O0FObtrLxsTEyMnNz9DMysPBvr2+vcDAyM3h+F1VVU9TUVpaX2ZucmleUUpDQD4+P0JJTFJWW1tcX2Fv+3ju7eLn9/h06tzOzMzcbE5FQURP/c2+uLW1ucDTXUc9Ojo8QEdPXuvWy8fGxsnJzMvKycXDwsLAwsTLzdXZ3eTscV5TT01QUlhfYmRcVU1JRkNDRUdKTU5PTk1NTlBUWV1iXGBeam9z/3ve1cvLy9XrXU5LS1V308W9u7u9x9xcSkA+PkBGTlx+29PKycfJy83S1tjVzsrHxMTFxsnN0djf8XBmXVlUTkxMTU5PT1BPTktLSk1LTlNcYlleV2Rm7urp/19OREA/RU/4zcC8u73E0mtOQ0BAQkpRYHzf1s7LysvOzs/NzcnFwb++vr6/xcjN0tne4Ofs+W1fWFFPTk1NS0xLSktMTU1OUFJRUVBOTk5QVllhamxnZGZjY2JiZmpwd3p8/np+/HBtamZnZWdqdfnw6OTf3NnW19fU0tLRzs3MzMzNz9Ta4PN6fe3p6+no4d7Z19rleF1STk9XbN/QycbI0PBVRj47OjxASVJj/erj4OTk4t7b2NPQzs7Nzc7O0NXc4Ont7vP4/XhtaGRfW1dXV1ZWVlZUUE5NTU9UWVxcW1paWlxjbXrx6+zs7O3u8e7p5+To5+rs7u7o4d3a2tvc3dvc3uDj4uHj5uTl5eLk4t7c2tnZ2tzd3+Dl7PH08/Ly+XdpXlZST09PU1dZXF5fYF5eXVtaWltcXF5fYWRqbm9sbnL+cvfm3d/j5evd1cvLyc7Y9GVdXnvczMS+vb7H2ltFPTk5Oz5BREdHR0dITFRl7tzRzMnHyMjHyMjIyMnLzM3Q0tXY3ODk7PT8fX16c21pY15aVU9NTU1PUFBQUFFUWFpcX2ZtcHVydXd99e3m3t7g4ufw9/r28Ozp6+vr7e3u93twbXB99u7o5+Xh39/f397e4eDk6/Z2amluc3h8+fr7e3BsaWdnaGlsbm9zfHx1dHRvbXR2c3VycHh+/Pf5eXVzbnB2e3Rxc3X/7+vs7evq8PH3/X57dnR1eXd4bmhnZWVoaWlqbnr27uvl4+Tk4+Xo6+/5d3Bra2xtbW5sbnR++e/q6uro6Ort7u77dXFycG1samVnZ2ZnamtmZWNhZmlqb3V89e7r6uXm5eDf39/e3uDj6e7y8vP39vDu7/l0bmxsa2hlZF9iaW5wc/317/T5+fPs6efj39/e3t7d3N3i5+ns8Pj09vr8+v52bWlmZGFfXVxbWVlaWVtcXF1eX2FqdHZ8fvrv6N/b2trb2t3g5Oru+H5za2loZ2VkYWFjZWdkX11eXl9iZWVpbHB8/vv59vr69vLw9fr5+O/r6ePh4N/f3tva293f4uns7u/3/n14c3FubG1sa2xyfvHr6+jl5OPl5unp6+zv7O34/f19c3V3d3h9dHh+/n58fHVzcnr9+Pt9fPx9eHZvb3Bydnf++3x5d3p+e3VwbWprbG1sbGttcG5ubnB0e3t6cm52d3BucG5ucHN1dXp+fnx2cGxrbW9ua2dqa250fX757+3s6ufo7PD09vl5bmhlZmhvcnf9/f757+zr7e/w7u3r6+zt7u7w8fX4fXZybm5vdnr99vPt6eXk5OHi4uHh4+Xo6uvs7vH3+vj37+/x7/L39vb4/3BqaWxpaWhiZGVmZmdqaWhrbHB6fXl1d33+/3p1cXFxcnBxdHFtbm5sbG1ucHV3eXl8+357fHp1bm5uamhqb3R3env/9/Xv7evn5ebk4uLg4ufs8vl+dnZ5dHR3fP35+fj1/X3++/z8+Pf4/Xx6/Pn5/P5+/vz69vLv8PHw7urn6Ors7Ozv+nlxb3BxcXh2b290/vjx7/D3+/n08O7v8vT18+3r6uvu8/j9e3p2b2xqbG1rampqbW9xcnNydnBwdHV2dnZvbW5vcnZ6e3d2eHd+/Xt8d3p7eXl3cW5tbGloZmNlZWhrbW93/fz7/ffw6+/z9f35+v5zb29udHr98/Dv7efh397e29vb3N7g5unr9nt6eHt9fXd0eXl8d3h7dXBub25yd29ra291dnF3e/jy8O/w7vH29fHx8fH4eG1sbGtsa2lscn7y7evp6urr7Orn6enn6Ors7vTx7/H4/nh4/vf8eXt9eHv7/3l7/H59/3Vwc25qam1taGdnZWdpaWhnaGZlY2ZscnFtbW1ubm54/fjy7ezt6uns8X51c21sbm1qaWtta21uc3v8+fPx8e/x8/X49/Dt6enq7O7t6+jq8fj6//317u/x8O7t6ujp5+bn6Ofm5uzv6+zt7/X5+fn++fPv7+7w9vl+ff37/XhybmxrbXFvbW1ubW5wbGhlZWRhX2BhYmNiZGRhYmZnZ2twffn18e7u8Ozq7O3v8/X2+v7+eXj++/bz7ujp7fL4+vt9cmxqZmFhYWBeXmBla211fPz07enp5+fl4eHl5eXo5+nt9fv9fv95dnV0d3r+//vx8Onn5+Xo6Ovv8vDu8PX5/X7/+vv8/fz7+Pb38O7u7Ovu8vp2cGtpZ2dlZGVmZmpwd3h0c3JzeX38+fr++fbv7e72/XRtaWZmZGVkZ2tubm95eXl6++/s7fH9c3BvamVjY2Jma250+/Ho5ebm5efq6+7u8fX18/Tz8/Lw+Pv59vPy9X59fnt+/f5+fnx3cG1tb3V+e3d0bm9xbW1ucnn+9fDs5eLh4eTm5eXk5ufo6err7O7u8fX8/v97d3FxdXV5/P18dnJycnBubGxtcHRvcHx7c25qaWhpaWtubmxudHZ6/ffy7u3u7/Tz/Hl0b21tbWxtb3R3ev3v7vDt6unq6uzw8vn49vv9/v59ff1+e3dvbm9tampqa2xtbHB5ent9//759vv9fP99fnx3dnR1eHx7ffr1+vf19fTw7/T0+Xp3eHd+/3x+8+vs6ODf4ePk5+fp7O3t8Pb29vT19/b09fT09fPv9v53bWxsamdkY2VqbW1udX349vPw8fj7/Xlvcnv8fHJwa2lqbG1sa2pqa2tw/Pv69vP4ff15fXx9+ff9/f358fLx9PH1/f53cnF3fXp4cXFwcnt5cm5vbWttbm1tc//8fv38/nt1en5+fXp7fn789e/u7u7p4t/c3N7f4OLl6PD8e3ZvamloaGdpbHB2evv18O7t6Obl5OLg4uTh4+jt83xxbmtnYmBfYGNlYmBeXFxdX2RlY2dub2xtbWpscHZ1c3b89e/t7evq5ODi4eDh5Obj5OXq7fD6+Pf5+nlvaWdpamtpaWtucHF2ev308PXx7+7y+Xtub3NwamhnZ2pvc3Fvdfz07+rl5eXj4uDi5ubm6/p7eHh1cnBsbW5ubG1vc337/fz18ezr6ejq7u/x+nt5enVzcW5pZmZnam94dnR2cXJ3efv39vl6ef73+Pn6/X199/Dr6+np7vDs6u37fXt4c21sa25wd/rx6+vq5ubl6Ovs7/R8b21rbnZ5d3BraWVhY2VkZmttbG10dXR0dHRsbGxpaWhra295end1cG9yffv28PPw7erq6OXi4N/g4ePk4+Xq7u7x9u7p5uHf4OLo7fP/dm5pZWFfYGRobXB5e3t3env9+/n07/L07/bz7u/7fHp3dnZ7fHp1bWxtbXBwamdvdnh59PH29vf5/fr6/n55e3t2cG5vbnF5fXp0eXh9/vz2+3pzbWpoa25tb3FydX728fDw7+7s7e/v7Ozu8/5+fnx8/v/77+ru8PP9d3N0dXFubXBvbnB1d3h0bmxub29wdHh7fHv9/Pz58+/q5OTk4+Dp6erl8+Pr9+3r2+Lc7+/27+7u6XxuXVpYW2Vy9Ovg3+Dl6+7+cGdgXFpZW1tbXV9kaG1zefn28PP5d3BucHp5enRyePnu6unq7O3y9/b38fHs6ujo7PL8/X58/nt0c3Z9eHv8e3Z2dW9rbG1ye3d0dnl9+vl8dXN3cHr49/5++fr88evq6enr5+Lh4uXn6/H19fj9e3hzcnV7e3VxcnV2/vPw+HZ6/ndubW1nYGFkY2NjaGdoaGptdX7+/Hx5e/z7e3hzb21uc3Nz/u/r7Orm4+Li4eTj4eDg3t3e4eHk5eTp7vx8fXp9/nlxb3R7/fX4/XxwcHRxbGpqaWNka21tcXRvcnVxc3VsaGdmZmdnZGdpam11fP78+fz+9/f18vt9e3Z2eXx5dn759/Xv6enq6+zw9vLw9Pf7/Px+eXV1d3NzdHBvdnx7eHb/+v3++fj07ezq5+Lh4+nr7O3s7+7w+Xl3ff53bm1sampud3h2fPv8+fT1+n7+eHFydXl0c3RyeP98dXBxdXV1e/98fPfu6+Xi4eDg4+fm5+vx/Xp5e3v7+f10bmtpaGZjX1xdXV5fYWFeXV5hZmdkYGNrdHb98u3m4+Hf3t3e4urv9vrz9Hhub3B3/Pry7e3s6uvr5ePn6Ovv7/Dv7/Lz8/b58vP59fLs7Onn6Ofp6+np6/R5c3JtamVgX2FiYV9gZWZlZmppaWZkZGVkZGNnamxpZ2hucW9xcHNxfHj3buf9bfzl2u3kbXpw4OTe53tjWl9eamNrZHrv3+De3uXp/PH57/b2enBudHv78vf0+O7o3trc3N3i5+bq7vr9ff/59Orr8Xxzbm1vdHl0cWhnaWptbWtoamxta2xubmxobm1uc3Z7dnF0eHRsampqaWtsbm9ydnVxbHL7e3h3ffr07/f09O/p5eTj4uXo6+/w9fp+c3J1fvfz8e/w9fn59/f8ffjw7u7s6urp6Onp5ujo7PP48/X4+vt+fHl0b2xtbmxjZGZiYGNlZGhqa3F5cXFvbWtra2xtbnF0dHzv7erm5ent7e7v9fj7eG52/fH1/Pj2+fr4+f39/n16/PX3/Pt9ffbv9/rw9vv+/nt4dnR1b29vePz4+Xx8fHp4d3RubGppa2ptd37/9Ozp49/h6Ozs6ujp7O3s7/h+fnxwcHZ6dHF1/Pb7+v51cnZ5ffx8c3d9fPf08/L39u3r7PL9cGxybmloa2hpbWpnaWljYmZrZGVnbW5qZ2Vqb3d2c3NtbXv+fvjv7vDu7Ofg4ufm5Ojp4ePi4N3d4+jg5Ovo4un2/vr++/ZxaGhranT9fnx7d3V+/nhxcHFvc3t9e3x+en76/nt5enZ5+PX9+/j39/v4+fx6b21rbG5wcXN8/vz39vZ+eHZwcHJyb3F1dXl8dm9wdXZucHz5+fv4+Pvw7vl0a2hmYWFkZWZnaGhrbXN7+PHw7uro5eLk5uTm6Onr6+zt7Ovt7+3w+f99fH3+/f/+fn348/Tw8Pb4+vHp7O7w8/T6fXZzcW1qa211cW9tbGtrbm90dXx+9/Xx7/Hy8PV8e37+e3n/+3p8/fz9fX77+Pf7+nz+9/Dw+Pj4/Hlwbmxsbm1tbGtwd/bu7u/t6+jl5+vv+nl5enJva2ZlaWxpam1ucnFvaGdoaGpoanBsbG5zd3lwbG1z//r18e/v7efi39/g4N/g4OLj6Ovq6/T6/nx5dHR3/PT6fHp7eHv+fH739vn59vTw7/T9fnl5fXduZ2Rlam1tb29wd3n+9fLx+fp+fHx2c3RwamRiYWRnam5vcXZ1ePz09/vz7+zn5Obo5uns7/r7e3JxdH758/P6+Pf17+/t7Ozu7urs7u7x9n5vbG51c3Bxb25ucnv79/Xx7+rp7fX3+3Z0dHBrZmZkYWJhZmlqb3V1d/9+//n4/nl6dXn+/3h7e3d+9u/t6OXi3t7e3t7e4erw93x2cWtnam51bGxsaGZnbXBua2lsc3h9+O/w8PH19/Tw7u3r7/Lt6ejp6+7t7fH1+nx6eXdycG1paWlpbG9xcHdzb29ubGxubm5ydHr7+fj5fHRxeH7+/HZwaWZmZ2dnaWhoa3F6/vTz+P398e3s7vHw8O/u7Ozw8O/w7Ono6O3u7ezs6ufl5ubk5enp7PT3/Hx++e7x+fb0+Pz18fT9fntxbGxpZGNjYmJiZWZmZWZnbXV3c3V1b21xdnp6cnFwcnV7+fTz9vfy7url4N/j4uTk5efs8fp7fHl2enhzeXpwcHV6/vX0+Pr7+fb6+vr/e3h2bmllZmhoaGloZmVkZGZpamxubW94+/D0+Pz9/vz2+vX4e3h7+/nz9vr5+vfw7+7u7/H09fPu7vL18u/t6+np6erq6+vp5+Tk5+fk5Obr8Pd+bWdjX1xdXVtaW15dXV5fYGJjaW9ubW90evnz8O/t7O3u8fn9/XtxbnZ9+vLy+fz38fDy8Ozr6uvs7Oro6/Dy7uno5eDh397d3t7f4+Tn6OzzfXFsaWlnZmZjYWFkaGpucHByefv28e3t8/p+dW5vbmpmaGtpZ2ZmaWxvdHp7fP307uvv/Hpzb3F3fHp6fvz7+PDz/f719Pv7fXZ6fXJsa2pqaGlqam52dGxqa29ydXd8+fTv6+nm5eTk5unp5urw9vr+/vn9/np1eP/8/fjx7u3p5+nr5+br7u70/31+fHVvb3B0evv7fXp6/fn7/P57d3V6eXp8dG5pZ2lrbGtnaWloa294enn88/Hv7Ozz8+vt9vn2+H13dW9sbnv49/b68Oji4ePo8fx+/X50b2tpbXf++PHr6uzo5eTo6+7y7+3u8fV9cm5qaGVmZ2VobHN3efx7d25ramhpaWtsbXB1fXx++/x+/PL0+Pj4fX59eHFyfvn3/H1+//7+/v7+/v7+/35+fn5+fn19fHx8fX19fX5+/////35+fv///wA="
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
                         var audioAppend = new
                        {
                            type = "input_audio_buffer.append",
                            audio = "UklGRgBXAABXQVZFZm10IBIAAAAHAAEAQB8AAEAfAAABAAgAAABmYWN0BAAAAKtWAABMSVNUGgAAAElORk9JU0ZUDQAAAExhdmY2MS4xLjEwMAAAZGF0YatWAAD//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////37///9+/////////////////37//35+////////fn7//37///9+fv//fv///////////////////35+fn5+fn5+fv/////////////+/v79/f7+/35+fv//fn19fX7///9+fv7+/v79/f7+/v39/f99e3p5eXp6enp6enx9fv9+//7+/v79/P3+///+/v5+fXx7enp7fHx8e3x8fv78+/z7+/v6+fn5+fn5+Pb09fX19PT09vj5+/v8/P39/fz8/n59fn3//f5+fXx5eHd0c3JycnJ0dXZ3eXl4eHh4eXh4d3Z3eXl5e3t6eXp7fXx8ff77+fr7/Pz8/P3+fX18e3t9//37+fn8/P3+/v7+/v99ff/+/Pz+fHx8fH59e3l5en78+fb08vLz9fX3+vr8/n59fX7++fj7+vXz8vP09fb29/n7/37+/v3+fnp2dHRzc3V3eHV0dnl9/v97eXl8ff79/Pv8/Pz7+vt+fHp7/fr49/j4+Pf48/L1+Pr8/Pn7/X16e/79/n59/n55eXx8eXd1c3N3dnRzcW9sa2prbG1ubm5wcXJ2eXt+/v7+/fz69vb49/Lv7evs7e/w7+/v7u7w9ff08O/u8PHw8O/t7fD19vf4+Pj+e3h3eXh3c3Jxb25wcnBwc3Z8+/Tw7/D5fXp4dnd0cHFwcXZ8/Pj4+vz/fv5+fHl4c3BvcHJxb25vcHBwc3J0eHp7enz8+/5+/v1+fX18fv99e3p4eHZ2ev34+vn19vHu7uzr6+rr7/T18/Dz+Pn5+fT09PX3+fr7+fj6+v17eHd7/P59enZyb29vcG1samlsbG1wdHV6fXd2e/7/fH758/Py8O7w8/T28/Hw8O7u7u3t7vLz8/j7+/f5fXt+/n14cnBwcHBwdXZ2enp0bWtrbWtra2tucnV4fHp4c3V0eH7+/nz//fv18O/w9P18dXJzb21sbGxvdXv88/Lw7ezr7vT3+/7++/f39/j37u3u7+7y8/Dy9/j4+Pn4+/z9/P39/fv8fn7//nl0b25zdXV5eXd2eHl+/fr7eXJzfXt7/ntxcXh7+e7r7O/x7+rp6+vv+Pv2+Pv6/nh1cnJycnBtbG1vcnd7fn58dnN6fHlxbm93ent7/fr8+fPv7Orq7e/v8/Tt6evs8fb39fPy+H59ffv07e7v8fTy8O7w9fT4//3+fHt2bmlpZWRkZWRlZWVpam1ta2lqbG9ubm1saWpucHd8fXt6fvz/+vl+d3V2dXR3fHx4eH3/9O/x8PDz9PLz9Pb7e3l+/vj19/jz7uzp6Ojp6ers6+ns7fDx9/76+P399vT2+/v29PDy9PLy8/Z+dnVzdHNxcXJvb25tcXd7/n18eXFxdnZwbmplZWhpaGhnY2FhYWVnaWxye/317ero5+Tl6Ojp6u3v9Pn8/v96c29vbnN7/vT0+vz18PD08/b19fb08O7u6+3y8e/v7ezq6+3v8vl9d3BvbWppaGpra2hoaGltcHJ0c3N2e/jz9fT2+fr28/n7/P38+/v9fndzdHV1env89/n39vb18/P09vjx7/Dx9fj5+/15dnZvbnFzdnx+/f59ffz7+/r+/f78+Pt9fXt1b2xramxsbGtsbnR9fXt7eX3+/Pj1+P58/f37+fn7/P19eXZ4//r49/r59e/u7+3t7uzt7ezq6uzt6+rt7vP19PX9eHFwbmxra2xtbm5tb29ycnN4d29tbG92dXV0cnR7/vv3+Pl+evz29fj6+3t2evz6fnx4cnJ7/nt9fvz38u3s6+vt7+/u7u3w9fz78vXz9/38/Pz39/n5+Pn5+PLu9HhubW9wbWtubm5tbHJ2fPv+fH5+/X7/+fb19Pb59fb4+/x9enl9fnh3dnl+/Hx2cW1qamxtbW9saGNiaGxvbmxtc//38/Hv8O7t7Ovu8Pf8/v7+fnp4dXN6/Pfw7O3t7evn5+fp6+zs7e7v8vT9+/T5fHhydHNwbWpsbW1ucnv/+PDv6+ns7O3v8e7s7+70+v16eHV0c3BtbnBwcXBtbW9tb3h4c3Fvb3h8eXZ0dnV2fvv18fl7d3t6eXd0b3B3ffnz8vl+d3d1dnd+/f317+3u8/n9fX58ff38+vx4cnFzcXJ1c3N1e/z07u3u6ujo5+bm6e3y9/b6e3p8/v7++vX0+Pj5/P5+/P97enp2dHRvbnZ5d3d2eXh8+/x+fv/8ff3+fXx6fH19fHl2dnV4fXt2/vx7dnR1cXBubmtqbG1wcm9vcXJ5fn5+d3p6eP/18fHw7/Dt7O7w8/bx7erp6Ofq6+nn6Ovs7vt2bm1ubW1rbHByeX779PHu7Ozr6uvt9Pj5/H7+fXd1bm1vcHBtbGtsbnV8eXZwbGtqampqaWpuc3v59O7w+fz++ff49PX6+/f08/b4+//+//9+eXl4ef38/Px8enz+//fy7u30+P59fv/+fnt3d3z79vPt7PDy7+7u8/j8fn57/v1+e3Ftbm9vb29ubm9yevv28/L18+/u7u7t9Pn59/Hz9/v6/f98/PX5fn7+/v19eHBvb25wb2xsa2pqaWpsa21ubGxvevn19ff3+n16//x7dXB0cnBwdv5+fXp6/fj58+/v7u3u6+3s7/v9fXh3d3l6eHl6fPv28/bz8fDs7O3v7+7w9vb6fXZ5eXn/+vf19Pb28/X19fX19vXv7+7s7e/z9fTx7/Lz8fP4+/3+fXp7enp6eXl1cG5pZWJgX19gYWVlZ2ttcHd+fXp8fXp4d3RvbnV2d3d0dnp7eXZ1c3BwcHN6eHJ1e/r18Ozu8vTz8vLw9Pr7fn5++/Pw7+7t7ezr6uzu8PDv7+/u8/j+d3d2dXR4enl5//79+/z8+37//X56e379fXr9+ft+/vf39Pj8/H56eHv+9/Pw7/Dv8fLv7vX+e3v9fHt4c21sa21wbm5zeH79/fj49ft6cnBwbW9tb3JwcHn29Pj6+fb1+fx9e3ZvcHR3dnN1d3d5fHx7e3J4fvXx9Pr+/nz+/fr19Pj59/b59/j9eXr++/bx7+/x8vDu7Ovu9Pl+/359enJubmloa21ucHR8+fTu6ujq6uvt7/Dw8/l9fn19fn17c25sa21vb25udHf++Pv+/Pv29vTy/Xlyb3Jvbm5ramtwdnz28fD5+fPy8/l+fnVsZ2ZnZmZmaGpudf3y7Onn5OLh4+Xo6/Dw8fX6e3RubXJ0c3BubW9yefn3+Pb19ff6+fXw8PL2/Pn08ff4+X54d3z39Pb18fHy8/j3+3x2b25wcXNvbG1uc/338/T28/Pu6+nr7fH29fX6fntwbW1xcG5qaWhpbXJ0cHB2fv379vbz8vf5+PX2+Pf4/fz5+Pj7fn59ev37+vf28vXz8O/u7u7w9v94fXlvbW5ubW5vcHZ6//56c3BvcG9ubGloZ2dpbHBubGttb29vbm92ffXu7u7v7Ojk4ubp6+3v7+/u7u7w+Pv7+PP09Pb08vny7u7y+fr6+37/eG5ta2lrbnZ5//r49PDs7O7v8/n8fX53cG5raWdmaGtsbnV+9+/r6urq6enq6+77c21ramtscHFydXx9/fr6+fXx9Pb08fPz+fz+e3RvbGttb3Byd337+fXy7evt7u/x9/17eHJtZ2RkZmltd3t7eHj+/Hx5e3Z4enp8fv78+fT08vL8eXNvb25wcHBubnBzdXv8+n51d/739fX29vT09PHx8u3q6uXg4ubo6urp6Ono5ePh5uvx+np2cWxnZGJiZGVmZ2lsc3p7fHp8fHx9//38/Pv6/Pr09PT6/3p4/vn4+Pr39fDu8PT3+3l1dnZ0dHh1dv33+Pn7+v57e3V0bWpqbHJ4eXZxbm9xbGllZGVmaGpsb3Z6ffrz9/Tw7+zr7e7u7uzt7vD4+/x7eP3+//v07+zn5ebm6Ovr6+zt8fLw9vx9eXJzcnB1e/34+vv4/f5+e3p5eXh3c3BxdG5rZ2NjY2RmbHj28erm5ufq6/D3/XpycG9vcXd6fX7//Pr19Pb29fP2+Pf6+vf4+/t+dnFqaGhscnN3eXj57uvn5eTk5ejq7fL+e3Jvbmxpamlrbm1tbWtqbnNydXh0b3F0cHBvbWxub29vc3728fDt6+ro6erp6ern6Ors7vT7+vT5/v368O719vP29/f5/Ht9fvn09/19fn14d3d2dXNvb3B1fHdzcnN2/vf6/3h0eXpwa2hqbnJ2d3l+/f78/v5+/X53efz28+/v8fTz8O3t7/f9fnp2eXhwbW1vePz18vTz8/Hx9fn6/X16eHNubW1ta2xubW5xc25vdX347+3u7/Hw8/t5eHNubmxoam91eX17e/n27+vq6Ojr6+np6u3w+Xt5fvx8eHh7fX59/v359vHu6+rr6+3t8vTw8PZ+dXV0dndzcnV4fP78+Pr6fHl1cXF0e3V2dnNweXx6enx7eX377+7t8PX09PLz+/r8/Pb0+nZtbHJ1c3h7d3FwbnJ3eHduamlpaGZnaWhoaWxwcXJ0dnhzdXd8/n59+vHt6ujm5ePj5+zv7/H3/nx4dnN0eHt8fvz4+fj39vHv6+zt7vL4+Pt+eXJxbWtsbXN99fPw8PHz9fr5/nl2cG5xdHt7dG5tc3R3fHz7+vby7Ojp6+nm5+Xi4eLp7vL3/354c25pZWltcHRva2pqbXh+/Pr7+vXy7vD5/n7+fXh4enh5/f39/Xl1cm9sa2lqaGdoZWhoaWtscXh4e3t6/fbx9fHw+fr08PX29vz3+Pf28vDx8fDu7e3u7+3u7+/w8PH19vb3+n18eHZ7fn16e314e31+/3369PDu7vDu7Ozv+nx3d3Rwb25samhnZ2ZjYGJna21ubnd7dnF1dHFzd3dyc3p8+fHw7u728u3t8PTz8/Pz8PDt7evt6+vr6Obm5+Xh3+Hl5Ojq6evv/Xx4dnp+fn18fXtzbWhjY2BeXl5fY2JjaGpucHV6eX76+ft8ef7+e3t6eHf8+ff6+vT4+P3+/v76/Pv5+PPv7erq6urs7O7zfnd0bm1qaWhkZGRnaWpsbnv89u7v6+jl6Onq6uvr7/b59e7u7e3t7O7w9fz7/Hh2c3Jwbm9uamhqamxramppampvef308O3r7Ozs7O3x8PDw9Pp4b21sa2tsa2xtb3l89vfz8PT59/399vTv+P17fH799ezj4N7h6fR9dnF99/Pv8PN7a19XUExKSkpMTk9TWl9pfOvf3tza1tDPzszLysvMztHRz83O0Nniemtoa3zt497e4O5uWU1CPDk4OTw/P0BCRk1f49LKx8XDwsHBwcLFx8nLzcvOzMzQ19vSz8rN1HpUR0NFTmTh18/S1+ZmTj42My4tLC8zPEVLTkxPUmPmzcK8uLa3tre5vcLM0NDKxsPFx8nR0dXe8HV7/trb23RSRD8+Q05l5NXY325URT0zMS8uLjA2PUxXZV5gX3jcx723tLCztLe5vb7Fyc3NycXCwsfJzc7U4e5w+ffb3d5pTT87Oj1GWPLX1tnwXEs+NjEuLC0tMDU+SmX74uHe2czCu7WwsLCxs7a4ur3ByMvNy8rJzc3R2+ZmVkxOT2L+el5JPDUyMzhCVuXQzdHsW0U7MS0sLCwuMzpM+9HKycjKyMK7tK+trK2tr7K3ur/Gzc/S0c7MztDR3+X6X1ROUVNjXllJPjczMjQ6RFfm08/Xd04+Ni4sLCwtLzU+V9rKw8PExMC9ubSwr6+vr6+xtbm+w8rO0NLP0dPe3Ot9b1hPSUtLVlVRSD02MS8wNj1M+9DKyM/xTD4zLiwrLC0xOkzpyr+9vL29vLm2sq+ura2trrG2u7/IztPc3Nvd6PLubG5fUUtISEhNS0g+ODEuLjA4QlzXyMPG0mxIOi8sKyorLTI8VNfDu7q5u7u6t7Wyr66ura2usbe9xMzS1uDk5+Dk5+B+7mdYT0tJSU5KRz04Mi8vMzpFXdjLx8vXZEc5MCspKiotMTxL3MK8t7i4ube1sq+urKysq6yusrjAytbo92ZfW2RdZfx09GlVR0VBQ0hHQz03MzEyNT1IYtjMxsjQdks7MC0rKistMDlP6Me9urq5uLi1s7Kwr66ura6wtrzAyM/W5HJrZ2dj6+Xe3XRYS0pJTUtFPjYxLy8yOURR683Fv8DK/Eg3LiopKywvNT5Zybmyr7C0tra2srCwsLCwr66vtLvG1OtoX1hPTE1LVHfp3fZWREA/QkZBOjQvLi82PElYeNLFvr3B30g3LiwpLC0uLzVC7bqwr7G2urq0sa+vs7OwrauqrrfAztjZ61lHQEFGVd/V1ONgSktPTkxBNzAvMDM5PD5FVtvDu73KaEE2MS4sKCkrLTVL2b6yr7Gwr7Gzsra6ubayr6yusLW8v8LNek1APEBMS1Jy8eje7VdRT0dBPjgzMzY3O0JLY8+/urm8zGhHOjAtKCUoKy40RXfDs66vr6+xtLW5vr66t7OwsLS2t7i9yu1PRUNGQj9GU/3i42Rdb+xgSTw1MTM2NTU5Ql/Mvr3AxtD7SjguKCUnKCorM0fPuLGvrquoqayxuLu4tbe6ura1tLS3vsnT6lZMQzs+SFZQTk5a6tnyTEA+Pjs2MC8zPUxg6czAvb/Lc0Q6LyonJSQlLThM3b60raalpaipq62wtry9vb7DxcC9wcnQ2OP3WUhHS09FQUJNYGBRR0lKSD42MzQ4OjxCUuTNzNPe+VM/Mi0oKCopKi5C8cK3rqqloaSoqamssbi/w8PFztfLxMva3t3mdkxJSU9LQD9IVlhZTExUVUs+Ozs9PT1BTe3QzMvKzOFVPjUvKykpKSovO1DQvLCrp6SjpKanqq2yub7GzNjj3ed0ZvF+aFhNVVJVSEJETU5IRkNIS0c+Ozs7Ozo9RVbv2M7LyM7wT0A2Ly4qKCouNDpOzrqwqqelo6OlqKutsri+ytLX3ud4bmhrbWdcTVxgXU5KTVNWTUtISk1JQz4+Pj09P0lZ+d7V0M/eYkg8My0rKSgoLDE6TNa9s6unpaSjpKeqrbK5vsnc7/x4aGJl9+zn8Gju8PhZTU1UVExJRUdJRkE9Pj4+PkJPZN7SzczO219GOzEuKygnKS4zPE7OvLCqqKelpKeprK+1vMLQ3t/q9e/lfHPn6PNfWmFcW01HRk1RTEdGTExKRD49P0BCR1J32tHQ0tjrVkM2MS8sKiouMztL5MW1rKuppqWnqqywtrrAz93d6Ovg3Ojt4ODp6l9YW2FVR0JETFBNSEhNT0xBPT1AREZIUv3SycrP2/VTQTkxLSssLS83QmbHt6+sqKWlp6isr7O6xdLb6vf48OTe3uLk7/xeU1ROTEVCQkdOTktISktLQz07Oz0+QkpY7tfNz91qU0Q5My4sKy0xN0R5yLmuq6mnpqiqrK+2u8DJ0dbZ1dLOzNHV3ultX1FKTExOSEdES1hgWlFPTU1IPjo5Oz9GTl3mz8jJ1PpYRTw0LisrLTE6R3TJt66qqKenqKmtsrrAyNHc7Pns3NTV2eD0b2FZS0VHR0dFQkFIVV1VTkxJSUdAOzo8QUpXbuPRycrU/VNEOTMvLSwuMztN38W5r6qop6iqrK2wtr3Fys7R1dna1dPX5W5cVlVLSUhIR0dHSE5bXVlTTUlGQj06OjxASll9287Jy9d7T0A3Mi8tLS83QV3Sv7atqqmpqautr7O6wMbKz9LX2NPO0tvpbV5ZV0tJSktKSUpJUF5iV09LSEZBPDg3Oj5HUmbq0szP4GRMQDkyMC4uLzdAWs69ta6qqKipqqyusrnAyc3V3eXv6+Tk+WxnY2hbWlpbWFRRT1RZXVVOTEpIQz46OjxARk1d+9vU2OxeTkI6MjAuLi40PUvryLuzraqqqqqsrrG3vcTIzNPX2NnW0tbk8nx9W1dXUk9NTk1VWlpTT1FOS0RAPDs9QEZOZefX0dTlYk9DODEvLSwtMjlFbMq6r6upqKioqq2xuL3Cytjl6OHk6vV+c/1kU1VUU0tJRktVWVJNT1JSTUZAPkBBQ0hSdt3V1tvqZkw9NDAuLCstMjpJ8Mm7r6qoqKipq6yvtry/x87V19fT1dfe3/xqcVxTSkpITldWUFRgXlxTTUhHRkJDSlpx6N7i62xUQzsxLi0rKi01PE3gw7mvq6qqqquvsba9w8bN3dvU0tjP0dHa4+RhW05MR0xTU1JUWlZZUEtFRUZDRU1bbOTd5vVeTT01MC0qKCstMDhIdMS2r66rqKqsr7O4u77HysfBxMXDwr/KzdPqVklEQEdOUUxi+vhwZ1hRV09MTmfw39ve62hRQjUuLSkmJiktNT5T1Lmvra2sq6ywt72/wMbMzMW+vb++vsfCzP1OS0NAR0tITGhjampeTk1NSUhPXmzm3+T/Xkk8My4sKScoLDE7SfbGtrCvr6+usLe9v7++vr68trS1t7e/wcLbTkhCPEdTT1Hx83R+W0hFRD5BSVrr0M7Q2HRLOzIsKSclJysxPl3Vv7Wwr7CzuLq+xsnDwby4t7OxtLm4xNLOdEdFRz5Ne+7fy9l0dU0+Ozo4QE1k07++v8psRDgtKCUkJCctN07Rvbi0tLa5vcfQ2NHKwbq1sa+ur7O7v81q7HxPSlFLYc/N0M3aX1RCODQ1Nz9T6Me7ur7KXjwwKyYkIiUqM0brzL61s7W5vsbCwcLBvLeyr6+wsrS5wtTcfffoaVdodmnk3eTp6lRHPzo3ODk+TXDTxcPM3FU7LywoJSUnKjJBV9nGv726u7y+vr25trOysbCwsLO3vcHGyNrg3ed1/m1ib3pnWFlORz47NzY3PEJNa9/Y2VxLQjUtKyopLC4wOEty0Ma/u7a1t7i6uLi4ubi2s7Gztri4urzH1Nvh+GdeWmf87vD1a1hNRD47Ojo9P0lSVE5PS0ZAOjYzNDQ2NjxGXOjSysK9vLu8vLy6urm3tra2tbW2uLq+xs3U4fh4ZV5fWlNSTUtKR0ZCPjw8PTw7PD1ARkpMTlFXWFVUUlNVVl5udurWzs3JxsHAv769vb28vb29vsDBxcnN0Njj9W1fVlBNSkdGRUZHQ0JERUREREVGSElKTU5RWl9dYWz86uPf29XS0tLPzczNzMzMy8nOzsvO1tPS19jZ6unj7+jz+2RqbWRUUVNaVk5UVltaXFtqY11bW2ZkXF1wb3Rne9faev3f6dzi7vrf6dvU28/Q4HTWz+j43P7u3/nrcmFeePD4aVdYbntkZlJPYfxoXVVXXGFocvVuZmnw5u5kWGtxcGdtaXbq3eDh3Nva3dnf9HVz8/BgYvVwX2338fvpeXni8F97Z198e3t8Z3fb4u7y8OPn5+/w7fhx6e97fvr79m5uempwcGt93udp/Nvc5On16+ni6+/f6fHg3Oz0dP91aFtcWl9nW151fm7x7P59e25vdHh8bG19//Pl3+pmfd3l82xo8HtkZ15t+m5Xbuh2X2z6dm1sem32+PF48Obq/u1xYWBweHNqc/rw5+fs9vl67PRvZvr49mlse/Xv+fz55OTk3+Tq3uZy9etxaPlvZGT17Gdje/T07/f8eOXkfvvu73F2921jcH778vzv+HPq6H5tbm1tbWxkbmJeZ3ZrbmFeenhoe/Fsdu3o+d/g8ffh4O/9cuTubu7wV2na5XFe4tjU4X30z+FmW/bzXE9aW15XVWR9ffZs+fL31eJncO/t4mpi/X5qZm705+5lb9vkatzhavLc+Gzd7GHi31bnznFq2HFn8d9tY2xqdtxvYu/m3HBv7/dxe2dvb2b7+G31d/j0//ne7O7v6O3q7Wlz4PFWZWVqX2ZdX2VgXH3ubnrta/DvfW5rZWp3ZGn0Zmnubl15fXR+9/L+3994deDu+Hzs7W3y39306Ofb4Onk6+3j4mh16+tm/mdt7PRsd25r8X5w/HJra/79cGRv9fLt8u379f3ffW1seG11d33tfejl4/fs/uj6bXz7bWd9ZWV1fWVsZGdw9fprZWVte2lkYF1iZ2pfZF5hY2xxeXv98+7w7+js6/R47vXw5+775+Pw7Ozo6Ojq6+zt8Pjy6ODr6+jj4uHn7u339fpuZHV5bGVnaWdhYmdnX2FsbW1wc3BzfPDv7/Pr6vT17ezw/HP69/Lu7vTv7e7z9vLx8fDu9fTy+n15d2xoZ2hnZ2dobGxsbHV8ef7t6uzt7+3w9vv8+v329/758fDw9P339fV+cWlpbmtsa2NjaWhnaWlnbXV1cG54/P56dXJ0efz07+nl5N/c3d3d4OLh5+vm5+ru8PT9/Ph+dXJyc3X9/25mYl9hYl9eX2FhYmNnZmhsb3J0en317e33dXBqZmdsbX3/cW30fmt3ysvcXVRjxbe4x1Y+OkBR4NXJv7WurLHFTTQuLS8wNTY5PkpRW1VLR0le3cfAu7i0srK1ub3CyMzKzMzO0dfeRknfd0tAPUu9srLNPi0rLTVBSWfFsqypsdA7LSgpKiwuMDdATE9MQENJVWvRwLWuq6yxuL2+vbi2tbWzsrCxucn3UEpNT09OUF5fSVLtSzYuLTjnxs9KNS8zOkFGRmLDsa2uvWQ8My8uLzA1PEZPTUZBPUhWe+jMvrGrq665vr+5uLe6urexr7K5xtxxaFZRT1dbYlxURjpGXUk3LzJJxb3VPjAvOT9JSk/cua6utcxTPzw3Mi4wN0BLST46PEdYYOPQwbuzsK6wtbu9vby8vLu4srCwuMDU52RUR0JFTVxcTUE9NjlISjszN0nKvtBGNTM7Pz49QW69sK+1v85wTTwzLzE5RExKQj5BSlZRWOrJvbeysK+xtbq7urm7vLq2s7O3vsfS7FZHQ0dMVFRLQj48ODQ7SEI5OD9lztdPPDc7Pz8+RFbPvLe3usDM7U49NzU4PUFDQ0BDS1JWV1vhxLu3tLOwr7C0uLu7u7u7urm4ub3H0vdaTklGRURGR0ZDPz07OT1GSEA/QUped15ORUZJSUlIRk520ca/vr/CydZpTEQ+PDo6OjtAS1lt69DJxr64tbOytLS0tri7v8LDwcDEyMzS3fddUExJSEVCQEBAQEBAPz9ESENARElPWlhTVFZbXlxbXFxt5tjNx8PCw8jO3m1XTEQ/PDw9QUZNWfbazL+8vLm2trW1tre5ury/wMLHycnQ2+V0YFdNSUVBQD89PT08PDw8PkE/P0NHTVVVVVleZmleW1pZYv7m1cvHxMTIzdbqZVFHQDw7PT9DSE5bbdnGwb+7uLi2tri5ubm6vL29wMPGzNPX5HpjVU5LSEVEQUA/Pz9BQkVHTUxISU5WYGdoanB6fm1lYl5bYG/w2c3JycvP2ONwV0pCPj09PkFHTll23s/Evr26uLi2tre4ubq8vcDCxsrN1N7l/F5VTUhFQ0A/Pz8/Pj9AQkNESExKSExRWGdtZ2lrY2FeXFtZWV5v5tLLycrM0Njhc1tPSURDQ0ZLUVxy5dLKxb66urq3t7e3uLq7vb7BxsrO2ux0XlhVTklGQ0FBQD8/P0BCQkRHSUlLTFFZWFZfZmzx6PD0fWplX1taWVpn8drLxsbHyc7W425YTUdEQkNGSk5YcuDPyMK+vLq5ubm6u7y+wMLGyc3R19vk+GxgWFFNTEtKSUdGRUVERUZGR0dHSElJTldTUVpaXHF+c31yYmRiXV5fXWRz6NXMysjKztXe+2BUTEhFREdNU1/+49XJw768u7u6uru7vL/BxMfKzdLX4/hyZVxYVE9NTEpJR0VFRkVFRURFR0lLTE9WWFVbYWT/5unp6PhxcmpiYWBibPjcz8vIyMvP1eJyW09JRkVGR0tQWWjv2tDLxcK/vr29vb2+wcHFyMfL09nh+2xhWVZUUFFSUVVUUE9NSUdGQ0JDQ0NFR0lMV2Jqcvz76d3c3Nvk83luZ2ZfXmZ+49fQzczN0dnkeF9UTElHR0hLUFts7dzPycTAvr6+vLy9vr/BwcPHys3V3eh6amddWllXV1hVUk9LSEdFQ0FAP0BAQ0ZJS09YW2BtcPjo6ezo7n1tZF1bWVpdZ/ng18/MzM7Q1uD1aFpVUE5RVVljeevaz8rGwsC/vr6+vsDDxMbJys3R1+F+aVxTUE1LTExMTU1LSUZDQkE/QEBBQ0VHTE9UYW958+3t4t7e3Nvk6+30+nhtdPbq3NPOysnKzNDb7HJcUU1KSUpNU1xp7trOycTCwL+/v7/Bw8XHyszP0tfe6PZuYltXVFJRUFFQT05OTU1MTEtLS0tLTE1OU1hZWl9pcHf38PL5fnNrZWNkY2d75dzV0M/R1t3tb19XUk5NT1NZYHbl18/LxcC/v7+/wMPExcrO1Nne5u3+bWhlXVhVUE5NTEtLS0pJS0tLTVFSUVRXWFtfZmdnbW1xev3+dG5raWhnaXH+6dzVz83Mzc7P1uDzb2BWUVFUV1tn+eTZz8rGw8DAwMHBwsPFyMvO0tjd5vxmXFdRTk5NTExNTEtLTExMS0tKSUlJSUlJS01QVVtjcH77+v95cG1scH3u5dzW0c/Pz9HU2N3l7vp1bnFsde/p5dvSzs3KycjIyMjJy8zNztDV297m+21gWlZTUE9OTU1NTU1OTk5NTU5NTk1MS0xNT1JXW19ocv3w6ODe3NrY19XU1dXW19na3N3g4+Li4N3Z19TQz87Nzc3Oz9DQ1tnZ293e5Or2eG5pZF9dWldTUE9OTU1LSUhISEhJSkxNUFRYXF5kaW5zc3v68uzo4d7d3NrZ2tjX1tXV0dHQz8/Pz9DS09PU19ja3d/j6Ovx/P92bGdjXVhVVVNRT09OTk5OTk9QUFFTVlpbX2NnbXn58Orq6ejo6ufm5+Xm5ubk5OTh3tvZ2NfV1dTT0tPV19fR3tTd0+nb49zl+37vXe5dXlxVYmJYXVtSfFRhUVRWWVFYTlpZWl1YYGplZ21t7nj08Ozl5e3m6enp7+/u8vDy9/by7+/t5ufk4+Th5enm5uPl6urp5+fq7PD1/H35/fv8+312c25samlnZ2BgYWJmamxsbW56fXt5+/r08uvp5+jm4t/e3dzf4OTo7vP4eG1oZWlpb3RxcnV2cW5tbnBwd3Z89vb49PH49/d3bnBxcnV0bWtsbmxtbnF3fPny7u/4/3h4enJwcnV2eHZ2cm9tbGtpbn3u6+zt6+rs7fJ9bm1sbW1ra2xubW5ub25wbnB1fvXy7+3v7/P07/T8dm5vdHZzef767+ro5uXk5ujm6Orr7vDx8e7s6Obn6+7t7ejo6Ovu8Pb+/H5+fX18e339/f97dndycnRzcG5sbGxrbW5ub29ta21vc3RwbmxucHN6eHBsbHBrbW5uamdtfF/u4vxWX3TWz9xhT1Zo3tPT19PQz9f7VEU/P0RRdNzOyMK/vsDJ2HNWTUtJSUlNU2Xu2tLNzc/T2uT4alxUUU9QVFxhdPrq5ebp/m9hXVpbXWZu+/Dr5uPg3+Pn5+fueW5uam168e7p6O/r6+v0+XV2efv89P1yb2tnZmhkZWx5+/Hq6eTf4efm5+zv7fZ+/PT19/Py6un0cGtmbPz1dXBvbn7r5+vx/Xf66uv7c2xucHH9/XVvdH7s5On4bmZmcff2d2lfXmJnaGNgXF1fZGxvaF1cX2h29/Z4amhx9+7q7npzd/bs5ef2dnJ3/eni5+3y7ebf29rd5unj4Nza2t7k3tvW1Nbd5uzx+n1wbnFxe/L6bWNYUVBUV1teXWBpbXh3Y1hQTElKTVFVWVxm8+TZ1dPQzcnIx8jLzcbCxsrU3tjNyszaWkQ8OT1JZdfJxsbI0PBOPDEsKSkrLzc+RVjz2s3JycvIycfCvrq2s7GwsLGzt7vAzNnvaVxXVFNVV1xeW1RQU1JKQz4+Qk9ke19IOzQyNT1W0r+6t7m9ye9NOzIuLS8yO0Bb4drO08zLxsPAv725trGurayusLa8wMbL1N90ZF5ZVk5LS0dKTlZRST88Pkdb8fRdQzcxLzQ+W86+ubm8yOxLOzQvLi4xNz1HU+nd0tTb1tXMx8C8uLazsbCvsLK1vL/GzdLe6XFkW1hUVllXXF5jV0tDQENOX/H7VkA3MTE2Qn3GurW2vMtjQjcwLy8wNDg+RlBh7trOysjGxsK+urezsbCxs7e6vsPIzdLY3eb3ZVdPTExKTVNfXE5HQEJJWXJ1XUg7NDAyOk7Yvre0tr/XTzw1MTI0ODw/R0xZaN7OycXGxsTBvbm0sa+wsrW5vMDDx8rLz9Pd625aU09OUFZiW01DPDs8Q0pNST84Mi8xOEb3w7m0tLvMWT40Ly8xNDc8PkNIVXDYysXCwL+8ubSwrq6ur7G1ub3BxMjKzM/Y5mlZTk5NTlRcX1FIQEBCSlFVTkQ7NTIzOENqy7y4t73PXkE5NDMzNTg6PT5GT2rZzcfEwL25tLGvrq6vsra6vsTIycvN0djrY1ZMSElLTVlmalRKQT5ASE9TS0I7NjQ2O0hxzb+7ur7LcUg7NjMzNTY4Oz5FUXjZzMfGw8C8t7Owr6+ws7a5vL6/wcLDxcrQ5GRWUVVZWWVhbVNKQj9CR01MQzw2MzI2PUz1zsK+vsLOfk1AOjY2NTU2ODs/Slv63NjQyL22sa6trq6wsra4u73Aw8TEx8vU4HNdVVdTU1paXExEPj5DSExMQTs1MzQ4Pklb7dfNyczXdk9BPDc2Njc5PEJKU2b95tXJwLq1sa+urq6wsra6vL2+wMHCx83Z7GVZWlpSVlRWT0dCP0FISkdAOzc2NztASFNj6tHKydD0UkQ8ODc3Nzg7P0ZOXf3dzsfAvLeyr66ur7K1uLq9v8LFyMnKztng7vHxbV5dUkxHPz4+QUNCPTo3Njc5PD5ETmnby8rO4WpNQz88Ojg4Oj1CTFr92s/IwLu2srCvsLGys7W4vL/DxcbHyM3S2ePr9mxka2FeU0tIR0dIRkE+PDs7Ojo7PEJOZePZ2tfqZFZJQT07Ojs9QklPZuzUxr66trOwr6+vsLO0t7q8v8PEyMrQ297qe2ZYVVdQTUpDQT8+Pz49PD09PD89OTpATFNYYV5x29nrblFJSEdHRT8/QkdTc+HPxL23s7GvsLGytbe5ury/w8jLzdLc6vtpYWZdV1ZPSklHRkZEQ0E+Pj08Ozs6PD9FTVls+Ovs/mhaUkxIRkVGSlBf9dXJv7u4tbW0tLW2uLm8vb7CxMXHysvP2N7pal1bVE1JRkNBQURDQUJBPj08Ozs8PD1ARkxYb/Pt8fZyX1ROS0tMUFhm7dfMxb+9ure3t7e4uru8vr/AwsXGyczN0trm+mxeV09MSUVBPz48PDs7Ozs8PDw+QENGS09UW19ja3J+dnBraWxse/vt3tbPycbBvry7u7q6urq8vb2+wcXJzc7W3e57YF5UTkxKR0VDQUJBPz8/Pj8/Pj4/QEJER0lOUFdbZm73697c2NPQz87My8jIyMXGxcTDxMPExsfHyMvMz9LV39zn8W9iXllRT1FOUUtNSktLTU1NS01OTVFOVlNeVlxbW2BUaFJpWm9g6Xbn6+Xb29XdztzM3tDXz9bQ29jX3+Pp3vrg5/j07uzm9Oh4ff5zd/z3aWpmemP0bmt1+V75YOxbamtcYV/9XfRn+m5v+mvqfejp5Ofs4uLd6vNm4mDlVONW01Pccuhr2+hw93DnZOZc3lrUamdi2VprbmB29u5e3V7XXeRf0VPnamtnfVhfe3Ri7Wbc5evy++576Vlqaf/tbP112V3tWOv/bF1p/lzfXHtd5mXtWGl4+XfxZufa4eHt5+7gaeF83XnrYut75Xvr/db78u7h7nxxZ99ifm7sbNpmdl75XHdbamfoe/Pr6dne2eTaeOV533b3ZOft6/9ubW98Ym3+7W3x/Olwe2Dt6/9nXmh6dVlpZG5oZmV1fWReYXx1/Gd27+t2e/rk3+tybWthbV9jaen+7+fh39zwYm9lcnh1YPXm5+3l9eDl82xdZ3htW2936efY4OTe/Gn8+2fv+Pjr6Hzn5nnx9W58fm56a27t7mrz5eTe6G1t5ed9X2Jw6Pxib+7j711eeerxZV936/JkXP7o6mpbZuLsdV9t++rraW575fDseN/Y5nj8++nj/3H+6en/Xmrw3Xtta3Xy6HdmefLp8nVZaHz3/mFbZl1ffXf54uv4/PXg5Xdu/n38a27q/vBtVVNwa2RlW1xncfrv7N3p/Onh5d/i+eHb2NbX4enqfXpiafXf4+br6ePs+F9kdHF18XJdXlxWUltq/PTr7Hx98P1oZnvy8XtrXW7i4Or+7eTf6nRp+9za7Wxu6uD+d/3w5+Hvb2756/dtZXDq5PdqbXl7cFxcdf76/V5ZaHrr7G5kb/Ph43lgZW10bF5gbnh8d2/56OHwePLs7Ox8bPXo6u/28n1pX15eb35kWl1r+PN0aHHt3NvncXTo4+DramR+7H1kX1pcZ31zdH7w8e7q7/j17ebd2trd4O34dXH97vN3bWx37uvl3d3c2dzc1tPZ5et76dzz4+dXbeZpbvRXX2xbXG1jYXBmZGtoaHNvbfd6bG1mZW9qYmBhY2lrZ2hfXmZoa3t9cfXq8PH0e/v1+/jx/ffo6+vp6+/o6/P16e7u497d2dvh+XBqc/19bmReWVxaYWVqZFxYVVxmcvnp6d7a2dzpaWFiZW1fXF1dYP3s6unj28/Jx8nMy8jJy8zP0M/DyuJSTlbOv8R5PDAuMz5b8NfNwsHD1005MTE3P0ZNQD9ESktSV2DSysTJx8e8uba2ub29u7y7wMXP1N/c7mtUSkRGTlzxe2VcRUVgakxDOjxkyMHYQC0qLDldz8bBwb+8wNdKODM3P1NrYU5JSVda19PX4dXMvLKxsru/wbu6tbi6vby+wMfV6lxSSktJTUxNS01NTUtKQT1EWFpOQDxCeMvIdT0vLTJD783ExMC8ubzNTjgxMzxPdWtYSEVKXejR0t/bzr62sbW5wMPAu7i4vMDDxMDAxtxdRT8/RE1aXVlXT1NPTEg+OkZpcGdHPUFuz8foQTEtLz/hwbm5ury6vspbOzEvNURfemZMRkda6tTMzM3Nwbu0srO7w8vIv7u5vcLIxsbG0XVLPz1ASFJfWlJKSEZEQ0Q+PlJzbFVGPkr7zcx7PzEtLz79wri2uLi6v9JOOTAwN0h72+xUSkdc2snIzNbRwbmxsLS8wsW/u7e4vMDGxcbI2V9FPTs+R1BcXlhOSkhHRUE7Pk5ZWUw/Pkx7ztBePTEtMUHuv7e1t7e7wd1HNi4tMj5T+vRcT1BV4s/Mzc/OwLexrrG4v8PBu7i4u7/GxsXHznNKPj0/TWP0+WFRTU9OUEs+RFNcXVZGSFzqz9hVOy8sLzxjw7e1tbe8x3tCNC0sLzhHZvNuX19w1dbR0NfUyL23r6+xub/KycrEwMLCwsLGy+1VQTw9RE5s63FdT0lHSkdHPD9MWVtYSEVb6srL6kU3LzI9W8e5tbW0ub3NWzswKywxPE5x6ebf2s/P0d9+8NrHu7a1tLm9wcbLy8vLx8XBwcPO4lpMRkdLVWJrZ1xTTktKS0lKPklUZltZRUda58rL3ko7MjY9WM+9uba2ub3SUzkuKiwwPE/64NjX08vNzdXs59jLwLu7ubq8vr7CxsvRzczHxsbQ3mZWUE9OUE5LTEtLS0pIREY+QVJgYFhLRFrox8fUUD0yMjlH3cC4tbK1uMPtQTQtLjI8TnHb1dDSzMzO2/xl99LFvLu6u7u9vb/Bx83V1dPSz9fkY1RMTExQUU5OTUxMTEhGREdJQFBgcWBjS1rpz8bObUM6MzpBacu8ubS0t7zOVzwyLjA3RWPf29rg3drl6WdlYt3Mvbq5vL/DxMDAv8fO29nUysvO5WBPTU5QWFFQSkpGSEVFQkJES1RUX2llVVBMVfjVzttbQzs3PUrsxrq1srO3v9xLOzQyNz5ObuLY19vc3+r4enzr08u8ubm7vcTAv7/AyM/Y2tXJzc7gcFRTT1VYU05JR0dMTE9KSEZHSlJUVFJPWFts8OL3bVxZX3Ls4uV0X1VVWV5mdnv66OHl82lWTEpMUFx66Ojn6OTe3trY2dXOysfGysPK3L68vMjjSfbLt7O+ajksKzZHybq0tbO3ucpPMygkJS06XXx1TkVASk5ZV0xPX9K+t7m7x9bZzsa/v8PGzMnGx87jVUhGS2Hg0tPeb1tQTUtJR0lLUmN4/XNhV1NTXGJkZGFdX2t97+vu8e/p39zZ2dvd3t3Z1tfZ2djX2Nfi3uTr/f117ePo8GxjXWN13tzd72tbWFVSUVFWW2z55+76Y11dX2V1//7z8efj4OXr/Xl1fPTu7PP+/vv/eWxoYGNjbHFxamFfX19fY15eX2dt/Pbr5ubr8/Do397c39/f3dzb2tnd4uTi4OLi5OPk4Of2dW9vbW1oZmtuaWpoY11cX19fW1lUU1RbZ33w7e3n5+ns9XVobXT57ufm6ezp4NnU0tjd4lXx0M3WekpQ5MW6wuxAMy86Tc+9uby9wcjXTzsvLC04SOzZ7llJR09n6dvq7t7Nv7q6vsfR0crBvLu+wMbFysrL5lJFQklx39PxVUVCQUpPTk5KS0xSUlFGQD5CTGL/+WdWVVhhbPt19/Le083MzdTc3d7Tz9HW2+Dc19bZ6WpcXm3s49/g7/v+/fTt6+rr5t/d3+b2c3N67ePg4uTr6vD39Ozp5uPk5ujxeG9xen7t7vb3dPtqXVBOTVpt/m1XS0VDSFNf+/XzcmpZUklCPjw/SFdk/mpqa+7c2dfY2tfLxb68u7/CyMvQ2N3p6efa3NTj4WRKdvPgeG5Ke9vCwNdKNi0sN0jMvLW3uL/D3U06Ly0vO03Y0NDtW1Nde+bZ6N3Txbu2t7m/yMrIwb29v8LD0MPGz35RRERNVHRZVEZDPkVFS0pGREJHSExERD4/RVBdaV5VVVFfav349Wv15NvR09vU193a3+ne2dXX429USkpPXuPOyMTFzNj8T0hGR09d7NfN09Po8nzu3tDKyMTFwcPCxsrX4Orl3NnZ2t/q9PBbT3BiZk9LRF7gyMjmSjYuLTM9dcu+vbzAyehJOC4rKy83R17y4/z79OPd3eHcz8a7t7Oztbq8wcK/wsPLztPLzs7jXk1DRUxi8NnifVZMRURBQEFBSE5aYWJXU1NRWV5ocXNnbfzp2Nre09fU2Nzs3NvV1/BgTUZFTmHWxL28vcHJ1WxOQTs5Oj5JXe3f0dXT29ri2trZ09LKyMLCwcPEx8rO19/t/GphWVZVVVZYWVpaWVlZVE5NQ0pNVFdWT1Nkd997WkU6NTU6Ql/Ww725ubvD2lQ+NzExNDpEV+XNwLy5ur3Ayc3Pysa/vLy2tra5vMLFydLXbVpNSEdNV3Lj4+H4blpNQz06ODo9QkxWX2psb337ZmReWVZTVFhq8dzZ3O1gUktJTFZw2szFwsLEytZ4U0Y9Ozk6Pkhd3szCvby8vb/Cxs3Kzs7OzcvHwb++wcTM2e5pW1JTU1ZYXGFdW1VORkI/Pj4/QUZKT1htc/rq5+n3bV9fXF1dXVtTUE1OUF373M/Lx8jKzdbjdF5PSkVCQ0ZNXOvPxb+8urq6u7y+xMfKztHU1dbQzszN0dr3X1FKR0dHSEtMTk1MS0lGQkE/P0BCR0xRW2f+5ujd3N/j6PF+/P71+Pn9cGllZ2334dTMyMbHys3V4HVbTklFQ0NFTV3r0se/vbu6uru8vsHFy8/Z3uPp6Ofk39ze5vVuXlVOS0hGRURDREVGSEpLTU5RVFNSUlNVV11eZXB5/fju7+32+nBtaF1YU1RWX/XbzsjDwMHDyM3Y8GFSTElJSlBd/tnMxb+9u7y8vsDFys3T2N7l7fj7fHx8+fhxZl1VTUlIRkNBQkFESExPU1lcYmZmYl5cWFRWWFlcYWhpePbt6+3zb2VfXFxjdunZzsnGxcjM1eVuWlJOTE5SXf/bzMW/vry7vL3Bxs3R2N7n9v5saGpvfPLs7vlsW1BJRUA/Pj4+P0JGTFFYYWdscGtjYVtZWlpfZXL77enp5+nt9vb4cGVfX15n/+fYzsfEwsPGzNTgd11VTkxNUVlu4tHKxcG/vr6/wMTGy8/Y3un+bWdjXlpWUU5MSkhHR0hJSktNUVdbXV9fXVtcWlhXV1dVVVVXWFtdXF1cXmFka3f06eHd2NTR0M3Mzc3O0tXX2dra2tra2tva1tTT1NPS0tDPzs7P0NLX3OPr7npsYFlWU1FPTk5NTExOTk9QT05OTk5NTUxNTU5PT1BRUlNVWV5lbXj16OPe29jW1dTU1dLQz9DR0dLS0M/Q0NHR0dHR0tTV2dzd4Obm5OLo7Pd0bW1sZV1aWVlZVFJSU1dbXlxaWVdWVldYV1hZW15kamxub21qcHd4evjs7O7u8O7t7Ozu8u7q5OHi4uTi393d3t7e3d7c2tra2+Lq7PH4fXlvbGtqaWpvcG1wdXZubHBvcXNva2NeXFtcXV1dW1xjZ2dnaWtuamtscHz77+rn5ePm6Ovv9vjw9P15eXlz/vTt5+Lf4ujt8Pby6eTh4ebl4uHj4Oju8vt+d3r4+XNtbXb06+Xq7fH1/XdzbGZgXV5kanP09fr59Pj2/W5lYF9dYGVpZl9dXGFpbnJ2dnj+/+/p5+jo7vPv+XlzdXj9+vr28erm5urs7/tydvv7/3398+zn6+32fm1jZmdrbG998Oro7vn59n11em9paGViYWlzendsaWtzdXFrY2BjbvHo5ebm6efg29nd6fp6fO3l5Ojm4+Xj3Nnd5n5qaG17fntxbmtyfPfx+Pl9dvzu6u37cGxsfvDn5ODh5+bw9vn9c21nY2Zna2tzb2NiY2VobGhoamtuamprcHp9fXR2ffDu+HlycP78dnRxeXFua3BtcHNwc3Bram54+/Tv7vby8/x8c3359fb3fvfw7O7u7u7v8+zq6Ofh4+bk5+ju9np3c251b3R0ff39+nxya2tpcf3u7Ozs7vPt6Ojn7v//8vDt7O3v7/N3bmhqanV4cnj28/ny9/5+/3BtbGhkXl5hZWhpamxxbnVudG9z//3z8uvt5+bm6Ojue3BqcHR+fP32+vh9d2ZfXFtcXWNpbW5wb3F2dv15enn57Obk6ez38Orh397e4OXl5ebj6vZuaGZpcvzv9vb17urp7f1rY19hbf38/fv/+f788fD4d3Zz/e7o6fB+c3BubW5vbm1oZ2xvdnt4dG5ubXj66+rk4d7d3Nzf4+jq8/x3enz68/Ly/H1xamJhZGZuePbt6vH+cGtnZ21nbWxraGhrbXp+9vz++fPr6OLg4unx/nNoZmZkZWlwcXv9ff93cHBta2xzfH39/nZua2ZnaWdmZ2dqa25wc3p+8O7r8PT4+vb4+nt2bm5wd/z16OTj5OXn6ujp5+fl6/Dx7+jg29zb3t7f6e739PHt6OPh4eXm5uzv+f15eXNtamRfX19janBvaGdmZmRmZGNjYmNmamxudXZwc3ZubGhmaW1yfPPu8fj7fn5+fnpzcnF2c/728vb2+Xl4fX7/fHlzaWpscHn4+e/t7fD5/Pt3c3Rvdnr06+nm4+jze3Nva2xtcn3z5+Lf3t/f4OHi4eTo6+zv9u7q6erwfXBwb2dye/Z2bGBeZXL0/WxZTklHS1f+2MvJyMvR4GtUSEE+PkBGT2Ll0cnGxMTHyc3P09HRzsvLyMrJy83T2u5pWU5NS0tNT1JVVllYVVJMR0NAQUNJUWD65uTl7nhlXVpYVFNbYG519ufXzMXCxMvdZU9MT17gyr+7uLm8xNB4TkI9Ojs+Rlfvz8bAv7/CyMvR0dXT0c7LycbHyMvQ3nVeUk5MTEtKSEdKTFBWW1lUT0tGQ0NCREZKTE5WWVhqfeXq+nJt8tvPzc7oXUtERUto0sC5tbW3vcroT0A6NzY4PUZW58vBvLy+wszS2djTzMbAvLu4ubm8wMnZ9GBYU1RSV1laXl5oYF5YT0pGQD8+PkBCREVGR0lLT1NWW1hUVFFWWVxjXPXr2tzb3NnRzcvP0O1jTkhLUXnSwry4uLm+y+ZSRT06OTxATnnPwr25ubu+xcvYz9XLy8bAv7u9vMLE0Nx1W1JKTEpMTVBQU1JOS0M/PDs7Oz5AR05XUFpaXV1UV1Bce9rPzNd2UENBQ07zy723tba7xdtRQDkzMjI2PEVb28m+u7u7vcDExsXCv727ube1t7a7vsfX6FpaTk9LTUhJSUpVU19dWE5IQT49PD0+QUNFSUtQW2hm+evb3e/2Z/nezMbEyt5aRUFATfTIurOvsLS8y2RGOzY0Mzc7RFjfysG+vr/EyMvNy8nGw7++vL26u73AzdZ4a1dTSUhGRktOX2JwYVVLRT89PDs9PUBCRUlISkpQUFRdcux2cF5g/tvOy8rVeE5DPkBMfsm7s6+vtbzNYkY7NjQ0NztEVuvPw7++v8LFyMfFwL68uLi2ubi8vL/DytfpWlRMTk5VXGRwd3xgWUtEPjw8PD5BRkhLS0pKSktOTVBcbPZw/m354NHNy9LyUkM/PUZe07+3srO3v9BbQzo0MzM3O0FObtrLxsTEyMnNz9DMysPBvr2+vcDAyM3h+F1VVU9TUVpaX2ZucmleUUpDQD4+P0JJTFJWW1tcX2Fv+3ju7eLn9/h06tzOzMzcbE5FQURP/c2+uLW1ucDTXUc9Ojo8QEdPXuvWy8fGxsnJzMvKycXDwsLAwsTLzdXZ3eTscV5TT01QUlhfYmRcVU1JRkNDRUdKTU5PTk1NTlBUWV1iXGBeam9z/3ve1cvLy9XrXU5LS1V308W9u7u9x9xcSkA+PkBGTlx+29PKycfJy83S1tjVzsrHxMTFxsnN0djf8XBmXVlUTkxMTU5PT1BPTktLSk1LTlNcYlleV2Rm7urp/19OREA/RU/4zcC8u73E0mtOQ0BAQkpRYHzf1s7LysvOzs/NzcnFwb++vr6/xcjN0tne4Ofs+W1fWFFPTk1NS0xLSktMTU1OUFJRUVBOTk5QVllhamxnZGZjY2JiZmpwd3p8/np+/HBtamZnZWdqdfnw6OTf3NnW19fU0tLRzs3MzMzNz9Ta4PN6fe3p6+no4d7Z19rleF1STk9XbN/QycbI0PBVRj47OjxASVJj/erj4OTk4t7b2NPQzs7Nzc7O0NXc4Ont7vP4/XhtaGRfW1dXV1ZWVlZUUE5NTU9UWVxcW1paWlxjbXrx6+zs7O3u8e7p5+To5+rs7u7o4d3a2tvc3dvc3uDj4uHj5uTl5eLk4t7c2tnZ2tzd3+Dl7PH08/Ly+XdpXlZST09PU1dZXF5fYF5eXVtaWltcXF5fYWRqbm9sbnL+cvfm3d/j5evd1cvLyc7Y9GVdXnvczMS+vb7H2ltFPTk5Oz5BREdHR0dITFRl7tzRzMnHyMjHyMjIyMnLzM3Q0tXY3ODk7PT8fX16c21pY15aVU9NTU1PUFBQUFFUWFpcX2ZtcHVydXd99e3m3t7g4ufw9/r28Ozp6+vr7e3u93twbXB99u7o5+Xh39/f397e4eDk6/Z2amluc3h8+fr7e3BsaWdnaGlsbm9zfHx1dHRvbXR2c3VycHh+/Pf5eXVzbnB2e3Rxc3X/7+vs7evq8PH3/X57dnR1eXd4bmhnZWVoaWlqbnr27uvl4+Tk4+Xo6+/5d3Bra2xtbW5sbnR++e/q6uro6Ort7u77dXFycG1samVnZ2ZnamtmZWNhZmlqb3V89e7r6uXm5eDf39/e3uDj6e7y8vP39vDu7/l0bmxsa2hlZF9iaW5wc/317/T5+fPs6efj39/e3t7d3N3i5+ns8Pj09vr8+v52bWlmZGFfXVxbWVlaWVtcXF1eX2FqdHZ8fvrv6N/b2trb2t3g5Oru+H5za2loZ2VkYWFjZWdkX11eXl9iZWVpbHB8/vv59vr69vLw9fr5+O/r6ePh4N/f3tva293f4uns7u/3/n14c3FubG1sa2xyfvHr6+jl5OPl5unp6+zv7O34/f19c3V3d3h9dHh+/n58fHVzcnr9+Pt9fPx9eHZvb3Bydnf++3x5d3p+e3VwbWprbG1sbGttcG5ubnB0e3t6cm52d3BucG5ucHN1dXp+fnx2cGxrbW9ua2dqa250fX757+3s6ufo7PD09vl5bmhlZmhvcnf9/f757+zr7e/w7u3r6+zt7u7w8fX4fXZybm5vdnr99vPt6eXk5OHi4uHh4+Xo6uvs7vH3+vj37+/x7/L39vb4/3BqaWxpaWhiZGVmZmdqaWhrbHB6fXl1d33+/3p1cXFxcnBxdHFtbm5sbG1ucHV3eXl8+357fHp1bm5uamhqb3R3env/9/Xv7evn5ebk4uLg4ufs8vl+dnZ5dHR3fP35+fj1/X3++/z8+Pf4/Xx6/Pn5/P5+/vz69vLv8PHw7urn6Ors7Ozv+nlxb3BxcXh2b290/vjx7/D3+/n08O7v8vT18+3r6uvu8/j9e3p2b2xqbG1rampqbW9xcnNydnBwdHV2dnZvbW5vcnZ6e3d2eHd+/Xt8d3p7eXl3cW5tbGloZmNlZWhrbW93/fz7/ffw6+/z9f35+v5zb29udHr98/Dv7efh397e29vb3N7g5unr9nt6eHt9fXd0eXl8d3h7dXBub25yd29ra291dnF3e/jy8O/w7vH29fHx8fH4eG1sbGtsa2lscn7y7evp6urr7Orn6enn6Ors7vTx7/H4/nh4/vf8eXt9eHv7/3l7/H59/3Vwc25qam1taGdnZWdpaWhnaGZlY2ZscnFtbW1ubm54/fjy7ezt6uns8X51c21sbm1qaWtta21uc3v8+fPx8e/x8/X49/Dt6enq7O7t6+jq8fj6//317u/x8O7t6ujp5+bn6Ofm5uzv6+zt7/X5+fn++fPv7+7w9vl+ff37/XhybmxrbXFvbW1ubW5wbGhlZWRhX2BhYmNiZGRhYmZnZ2twffn18e7u8Ozq7O3v8/X2+v7+eXj++/bz7ujp7fL4+vt9cmxqZmFhYWBeXmBla211fPz07enp5+fl4eHl5eXo5+nt9fv9fv95dnV0d3r+//vx8Onn5+Xo6Ovv8vDu8PX5/X7/+vv8/fz7+Pb38O7u7Ovu8vp2cGtpZ2dlZGVmZmpwd3h0c3JzeX38+fr++fbv7e72/XRtaWZmZGVkZ2tubm95eXl6++/s7fH9c3BvamVjY2Jma250+/Ho5ebm5efq6+7u8fX18/Tz8/Lw+Pv59vPy9X59fnt+/f5+fnx3cG1tb3V+e3d0bm9xbW1ucnn+9fDs5eLh4eTm5eXk5ufo6err7O7u8fX8/v97d3FxdXV5/P18dnJycnBubGxtcHRvcHx7c25qaWhpaWtubmxudHZ6/ffy7u3u7/Tz/Hl0b21tbWxtb3R3ev3v7vDt6unq6uzw8vn49vv9/v59ff1+e3dvbm9tampqa2xtbHB5ent9//759vv9fP99fnx3dnR1eHx7ffr1+vf19fTw7/T0+Xp3eHd+/3x+8+vs6ODf4ePk5+fp7O3t8Pb29vT19/b09fT09fPv9v53bWxsamdkY2VqbW1udX349vPw8fj7/Xlvcnv8fHJwa2lqbG1sa2pqa2tw/Pv69vP4ff15fXx9+ff9/f358fLx9PH1/f53cnF3fXp4cXFwcnt5cm5vbWttbm1tc//8fv38/nt1en5+fXp7fn789e/u7u7p4t/c3N7f4OLl6PD8e3ZvamloaGdpbHB2evv18O7t6Obl5OLg4uTh4+jt83xxbmtnYmBfYGNlYmBeXFxdX2RlY2dub2xtbWpscHZ1c3b89e/t7evq5ODi4eDh5Obj5OXq7fD6+Pf5+nlvaWdpamtpaWtucHF2ev308PXx7+7y+Xtub3NwamhnZ2pvc3Fvdfz07+rl5eXj4uDi5ubm6/p7eHh1cnBsbW5ubG1vc337/fz18ezr6ejq7u/x+nt5enVzcW5pZmZnam94dnR2cXJ3efv39vl6ef73+Pn6/X199/Dr6+np7vDs6u37fXt4c21sa25wd/rx6+vq5ubl6Ovs7/R8b21rbnZ5d3BraWVhY2VkZmttbG10dXR0dHRsbGxpaWhra295end1cG9yffv28PPw7erq6OXi4N/g4ePk4+Xq7u7x9u7p5uHf4OLo7fP/dm5pZWFfYGRobXB5e3t3env9+/n07/L07/bz7u/7fHp3dnZ7fHp1bWxtbXBwamdvdnh59PH29vf5/fr6/n55e3t2cG5vbnF5fXp0eXh9/vz2+3pzbWpoa25tb3FydX728fDw7+7s7e/v7Ozu8/5+fnx8/v/77+ru8PP9d3N0dXFubXBvbnB1d3h0bmxub29wdHh7fHv9/Pz58+/q5OTk4+Dp6erl8+Pr9+3r2+Lc7+/27+7u6XxuXVpYW2Vy9Ovg3+Dl6+7+cGdgXFpZW1tbXV9kaG1zefn28PP5d3BucHp5enRyePnu6unq7O3y9/b38fHs6ujo7PL8/X58/nt0c3Z9eHv8e3Z2dW9rbG1ye3d0dnl9+vl8dXN3cHr49/5++fr88evq6enr5+Lh4uXn6/H19fj9e3hzcnV7e3VxcnV2/vPw+HZ6/ndubW1nYGFkY2NjaGdoaGptdX7+/Hx5e/z7e3hzb21uc3Nz/u/r7Orm4+Li4eTj4eDg3t3e4eHk5eTp7vx8fXp9/nlxb3R7/fX4/XxwcHRxbGpqaWNka21tcXRvcnVxc3VsaGdmZmdnZGdpam11fP78+fz+9/f18vt9e3Z2eXx5dn759/Xv6enq6+zw9vLw9Pf7/Px+eXV1d3NzdHBvdnx7eHb/+v3++fj07ezq5+Lh4+nr7O3s7+7w+Xl3ff53bm1sampud3h2fPv8+fT1+n7+eHFydXl0c3RyeP98dXBxdXV1e/98fPfu6+Xi4eDg4+fm5+vx/Xp5e3v7+f10bmtpaGZjX1xdXV5fYWFeXV5hZmdkYGNrdHb98u3m4+Hf3t3e4urv9vrz9Hhub3B3/Pry7e3s6uvr5ePn6Ovv7/Dv7/Lz8/b58vP59fLs7Onn6Ofp6+np6/R5c3JtamVgX2FiYV9gZWZlZmppaWZkZGVkZGNnamxpZ2hucW9xcHNxfHj3buf9bfzl2u3kbXpw4OTe53tjWl9eamNrZHrv3+De3uXp/PH57/b2enBudHv78vf0+O7o3trc3N3i5+bq7vr9ff/59Orr8Xxzbm1vdHl0cWhnaWptbWtoamxta2xubmxobm1uc3Z7dnF0eHRsampqaWtsbm9ydnVxbHL7e3h3ffr07/f09O/p5eTj4uXo6+/w9fp+c3J1fvfz8e/w9fn59/f8ffjw7u7s6urp6Onp5ujo7PP48/X4+vt+fHl0b2xtbmxjZGZiYGNlZGhqa3F5cXFvbWtra2xtbnF0dHzv7erm5ent7e7v9fj7eG52/fH1/Pj2+fr4+f39/n16/PX3/Pt9ffbv9/rw9vv+/nt4dnR1b29vePz4+Xx8fHp4d3RubGppa2ptd37/9Ozp49/h6Ozs6ujp7O3s7/h+fnxwcHZ6dHF1/Pb7+v51cnZ5ffx8c3d9fPf08/L39u3r7PL9cGxybmloa2hpbWpnaWljYmZrZGVnbW5qZ2Vqb3d2c3NtbXv+fvjv7vDu7Ofg4ufm5Ojp4ePi4N3d4+jg5Ovo4un2/vr++/ZxaGhranT9fnx7d3V+/nhxcHFvc3t9e3x+en76/nt5enZ5+PX9+/j39/v4+fx6b21rbG5wcXN8/vz39vZ+eHZwcHJyb3F1dXl8dm9wdXZucHz5+fv4+Pvw7vl0a2hmYWFkZWZnaGhrbXN7+PHw7uro5eLk5uTm6Onr6+zt7Ovt7+3w+f99fH3+/f/+fn348/Tw8Pb4+vHp7O7w8/T6fXZzcW1qa211cW9tbGtrbm90dXx+9/Xx7/Hy8PV8e37+e3n/+3p8/fz9fX77+Pf7+nz+9/Dw+Pj4/Hlwbmxsbm1tbGtwd/bu7u/t6+jl5+vv+nl5enJva2ZlaWxpam1ucnFvaGdoaGpoanBsbG5zd3lwbG1z//r18e/v7efi39/g4N/g4OLj6Ovq6/T6/nx5dHR3/PT6fHp7eHv+fH739vn59vTw7/T9fnl5fXduZ2Rlam1tb29wd3n+9fLx+fp+fHx2c3RwamRiYWRnam5vcXZ1ePz09/vz7+zn5Obo5uns7/r7e3JxdH758/P6+Pf17+/t7Ozu7urs7u7x9n5vbG51c3Bxb25ucnv79/Xx7+rp7fX3+3Z0dHBrZmZkYWJhZmlqb3V1d/9+//n4/nl6dXn+/3h7e3d+9u/t6OXi3t7e3t7e4erw93x2cWtnam51bGxsaGZnbXBua2lsc3h9+O/w8PH19/Tw7u3r7/Lt6ejp6+7t7fH1+nx6eXdycG1paWlpbG9xcHdzb29ubGxubm5ydHr7+fj5fHRxeH7+/HZwaWZmZ2dnaWhoa3F6/vTz+P398e3s7vHw8O/u7Ozw8O/w7Ono6O3u7ezs6ufl5ubk5enp7PT3/Hx++e7x+fb0+Pz18fT9fntxbGxpZGNjYmJiZWZmZWZnbXV3c3V1b21xdnp6cnFwcnV7+fTz9vfy7url4N/j4uTk5efs8fp7fHl2enhzeXpwcHV6/vX0+Pr7+fb6+vr/e3h2bmllZmhoaGloZmVkZGZpamxubW94+/D0+Pz9/vz2+vX4e3h7+/nz9vr5+vfw7+7u7/H09fPu7vL18u/t6+np6erq6+vp5+Tk5+fk5Obr8Pd+bWdjX1xdXVtaW15dXV5fYGJjaW9ubW90evnz8O/t7O3u8fn9/XtxbnZ9+vLy+fz38fDy8Ozr6uvs7Oro6/Dy7uno5eDh397d3t7f4+Tn6OzzfXFsaWlnZmZjYWFkaGpucHByefv28e3t8/p+dW5vbmpmaGtpZ2ZmaWxvdHp7fP307uvv/Hpzb3F3fHp6fvz7+PDz/f719Pv7fXZ6fXJsa2pqaGlqam52dGxqa29ydXd8+fTv6+nm5eTk5unp5urw9vr+/vn9/np1eP/8/fjx7u3p5+nr5+br7u70/31+fHVvb3B0evv7fXp6/fn7/P57d3V6eXp8dG5pZ2lrbGtnaWloa294enn88/Hv7Ozz8+vt9vn2+H13dW9sbnv49/b68Oji4ePo8fx+/X50b2tpbXf++PHr6uzo5eTo6+7y7+3u8fV9cm5qaGVmZ2VobHN3efx7d25ramhpaWtsbXB1fXx++/x+/PL0+Pj4fX59eHFyfvn3/H1+//7+/v7+/v7+/35+fn5+fn19fHx8fX19fX5+/////35+fv///wA="
                        };
                        await SendToWebSocketAsync(openAiWebSocket, audioAppend);
                        
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
    
    private async Task SendSessionUpdateAsync(WebSocket openAiWebSocket, Domain.AISpeechAssistant.AiSpeechAssistant assistant, string prompt)
    {
        // var configs = await InitialSessionConfigAsync(assistant).ConfigureAwait(false);
        
        var sessionUpdate = new
        {
            type = "session.update",
            session = new
            {
                turn_detection = new { type = "server_vad" },
                input_audio_format = "g711_ulaw",
                output_audio_format = "g711_ulaw",
                voice = string.IsNullOrEmpty(assistant.Voice) ? "alloy" : assistant.Voice,
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