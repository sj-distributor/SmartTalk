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
using SmartTalk.Core.Constants;
using Microsoft.AspNetCore.Http;
using SmartTalk.Messages.Constants;
using SmartTalk.Core.Services.Jobs;
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
using JsonSerializer = System.Text.Json.JsonSerializer;
using RecordingResource = Twilio.Rest.Api.V2010.Account.Call.RecordingResource;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public interface IAiSpeechAssistantService : IScopedDependency
{
    CallAiSpeechAssistantResponse CallAiSpeechAssistant(CallAiSpeechAssistantCommand command);

    Task<AiSpeechAssistantConnectCloseEvent> ConnectAiSpeechAssistantAsync(ConnectAiSpeechAssistantCommand command, CancellationToken cancellationToken);

    Task RecordAiSpeechAssistantCallAsync(RecordAiSpeechAssistantCallCommand command, CancellationToken cancellationToken);

    Task ReceivePhoneRecordingStatusCallbackAsync(ReceivePhoneRecordingStatusCallbackCommand command, CancellationToken cancellationToken);
    
    Task TransferHumanServiceAsync(TransferHumanServiceCommand command, CancellationToken cancellationToken);

    Task HangupCallAsync(string callSid, CancellationToken cancellationToken);
}

public class AiSpeechAssistantService : IAiSpeechAssistantService
{
    private readonly OpenAiSettings _openAiSettings;
    private readonly TwilioSettings _twilioSettings;
    private readonly ZhiPuAiSettings _zhiPuAiSettings;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    public AiSpeechAssistantService(
        OpenAiSettings openAiSettings,
        TwilioSettings twilioSettings,
        ZhiPuAiSettings zhiPuAiSettings,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _openAiSettings = openAiSettings;
        _twilioSettings = twilioSettings;
        _zhiPuAiSettings = zhiPuAiSettings;
        _backgroundJobClient = backgroundJobClient;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
    }

    public CallAiSpeechAssistantResponse CallAiSpeechAssistant(CallAiSpeechAssistantCommand command)
    {
        var response = new VoiceResponse();
        var connect = new Connect();

        connect.Stream(url: $"wss://{command.Host}/api/AiSpeechAssistant/connect/{command.From}/{command.To}");
        
        response.Append(connect);

        var twiMlResult = Results.Extensions.TwiML(response);

        return new CallAiSpeechAssistantResponse { Data = twiMlResult };
    }

    public async Task<AiSpeechAssistantConnectCloseEvent> ConnectAiSpeechAssistantAsync(ConnectAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        Log.Information($"The call from {command.From} to {command.To} is connected");

        var (assistant, knowledgeBase) = await BuildingAiSpeechAssistantKnowledgeBaseAsync(command.From, command.To, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(knowledgeBase)) return new AiSpeechAssistantConnectCloseEvent();

        var humanContact = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantHumanContactByAssistantIdAsync(assistant.Id, cancellationToken).ConfigureAwait(false);

        var openaiWebSocket = await ConnectOpenAiRealTimeSocketAsync(assistant, knowledgeBase, cancellationToken).ConfigureAwait(false);
        
        var context = new AiSpeechAssistantStreamContxtDto
        {
            Host = command.Host,
            LastPrompt = knowledgeBase,
            HumanContactPhone = humanContact.HumanPhone,
            LastUserInfo = new AiSpeechAssistantUserInfoDto
            {
                PhoneNumber = command.From
            }
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
        Log.Information($"Handling receive phone record: {@command}");
        
        TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);
        var callResource = await CallResource.FetchAsync(pathSid: command.CallSid).ConfigureAwait(false);
        Log.Information($"Fetched call resource: {@callResource}");
        
        var aiSpeechAssistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByNumbersAsync(callResource.To, cancellationToken).ConfigureAwait(false);

        if (aiSpeechAssistant == null) return;

        _backgroundJobClient.Enqueue<IMediator>(x => x.SendAsync(new ReceivePhoneOrderRecordCommand
        {
            RecordUrl = command.RecordingUrl,
            AgentId = aiSpeechAssistant.AgentId,
            CreatedDate = callResource.StartTime.Value
        }, cancellationToken));
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

    private async Task<(Domain.AISpeechAssistant.AiSpeechAssistant Assistant, string Prompt)> BuildingAiSpeechAssistantKnowledgeBaseAsync(string from, string to, CancellationToken cancellationToken)
    {
        var (assistant, promptTemplate, userProfile) = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantInfoByNumbersAsync(from, to, cancellationToken).ConfigureAwait(false);
        
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
        await SendSessionUpdateAsync(openAiWebSocket, prompt).ConfigureAwait(false);
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
    
    private async Task ReceiveFromTwilioAsync(WebSocket twilioWebSocket, WebSocket openAiWebSocket, AiSpeechAssistantStreamContxtDto context)
    {
        var buffer = new byte[1024 * 10];
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
                            await SendToWebSocketAsync(openAiWebSocket, audioAppend);
                            break;
                        case "stop":
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

    private async Task SendToTwilioAsync(WebSocket twilioWebSocket, WebSocket openAiWebSocket, AiSpeechAssistantStreamContxtDto context, CancellationToken cancellationToken)
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
                    
                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "response.done")
                    {
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
                                        case OpenAiToolConstants.UpdateOrder:
                                            await ProcessUpdateOrderAsync(openAiWebSocket, context, outputElement, cancellationToken).ConfigureAwait(false);
                                            break;

                                        case OpenAiToolConstants.RepeatOrder:
                                            await ProcessRepeatOrderAsync(openAiWebSocket, context, outputElement, cancellationToken).ConfigureAwait(false);
                                            break;

                                        case OpenAiToolConstants.ConfirmOrder:
                                            await ProcessOrderAsync(openAiWebSocket, context, outputElement, cancellationToken).ConfigureAwait(false);
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

                    if (!context.InitialConversationSent)
                    {
                        await SendInitialConversationItem(openAiWebSocket);
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
    
    private async Task ProcessOrderAsync(WebSocket openAiWebSocket, AiSpeechAssistantStreamContxtDto context, JsonElement jsonDocument, CancellationToken cancellationToken)
    {
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

    private async Task ProcessHangupAsync(WebSocket openAiWebSocket, AiSpeechAssistantStreamContxtDto context, JsonElement jsonDocument, CancellationToken cancellationToken)
    {
        var goodbye = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "function_call_output",
                call_id = jsonDocument.GetProperty("call_id").GetString(),
                output = "Reply in the customer's language: Goodbye and have a nice day"
            }
        };
        
        await SendToWebSocketAsync(openAiWebSocket, goodbye);
        await SendToWebSocketAsync(openAiWebSocket, new { type = "response.create" });
        
        _backgroundJobClient.Schedule<IAiSpeechAssistantService>(x => x.HangupCallAsync(jsonDocument.GetProperty("call_id").GetString(), cancellationToken), TimeSpan.FromSeconds(2));
    }
    
    private async Task ProcessTransferCallAsync(WebSocket openAiWebSocket, AiSpeechAssistantStreamContxtDto context, JsonElement jsonDocument, string functionName, CancellationToken cancellationToken)
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
    
    private async Task ProcessRepeatOrderAsync(WebSocket openAiWebSocket, AiSpeechAssistantStreamContxtDto context, JsonElement jsonDocument, CancellationToken cancellationToken)
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
    
    private async Task ProcessUpdateOrderAsync(WebSocket openAiWebSocket, AiSpeechAssistantStreamContxtDto context, JsonElement jsonDocument, CancellationToken cancellationToken)
    {
        Log.Information("Ai phone order items: {@items}", jsonDocument.GetProperty("arguments").ToString());
        
        context.OrderItems = JsonConvert.DeserializeObject<AiSpeechAssistantOrderDto>(jsonDocument.GetProperty("arguments").ToString());
        
        var orderItemsJson = JsonConvert.SerializeObject(context.OrderItems).Replace("after_modified_order_items", "current_order");
        
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
    
    private async Task HandleSpeechStartedEventAsync(WebSocket twilioWebSocket, WebSocket openAiWebSocket, AiSpeechAssistantStreamContxtDto context)
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

    private async Task SendInitialConversationItem(WebSocket openaiWebSocket)
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
                        text = "Greet the user with: 'Hello Moon house, Santa Monica.'"
                    }
                }
            }
        };

        await SendToWebSocketAsync(openaiWebSocket, initialConversationItem);
        await SendToWebSocketAsync(openaiWebSocket, new { type = "response.create" });
    }
    
    private async Task SendMark(WebSocket twilioWebSocket, AiSpeechAssistantStreamContxtDto context)
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
    
    private async Task SendSessionUpdateAsync(WebSocket openAiWebSocket, string prompt)
    {
        var sessionUpdate = new
        {
            type = "session.update",
            session = new
            {
                turn_detection = new { type = "server_vad" },
                input_audio_format = "g711_ulaw",
                output_audio_format = "g711_ulaw",
                voice = "alloy",
                instructions = prompt,
                modalities = new[] { "text", "audio" },
                temperature = 0.8,
                tools = new[]
                {
                    new OpenAiRealtimeToolDto
                    {
                        Type = "function",
                        Name = OpenAiToolConstants.UpdateOrder,
                        Description = "When the customer modifies the dishes in the current order, for example, [I want a portion of Kung Pao scallops], [I don’t want the beef I just ordered], [I want ice in the Coke]",
                        Parameters = new OpenAiRealtimeToolParametersDto
                        {
                            Type = "object",
                            Properties = new
                            {
                                after_modified_order_items = new
                                {
                                    type = "array",
                                    description = "The current complete order after the guest has modified the order",
                                    items = new
                                    {
                                        type = "object",
                                        properties = new
                                        {
                                            item_name = new
                                            {
                                                type = "string",
                                                description = "Name of the item ordered"
                                            },
                                            quantity = new
                                            {
                                                type = "number",
                                                description = "New quantity for the item"
                                            },
                                            price = new
                                            {
                                                type = "string",
                                                description = "The price of the item multiplied by the quantity"
                                            },
                                            notes = new
                                            {
                                                type = "string",
                                                description = "Additional notes or specifications for the item"
                                            },
                                            specification = new
                                            {
                                                type = "string",
                                                description = "Specified item size, such as large, medium, and small"
                                            }
                                        }
                                    }
                                },
                                total_price = new
                                {
                                    type = "number",
                                    description = "The total price of the customer order",
                                }
                            }
                        }
                    },
                    new OpenAiRealtimeToolDto
                    {
                        Type = "function",
                        Name = OpenAiToolConstants.RepeatOrder,
                        Description = "The customer needs to repeat the order."
                    },
                    new OpenAiRealtimeToolDto
                    {
                        Type = "function",
                        Name = OpenAiToolConstants.TransferCall,
                        Description = "Triggered when the customer requests to transfer the call to a real person, or when the customer is not satisfied with the current answer and wants someone else to serve him/her"
                    },
                    new OpenAiRealtimeToolDto
                    {
                        Type = "function",
                        Name = OpenAiToolConstants.HandlePhoneOrderIssues,
                        Description = "Resolve inquiries or issues related to orders placed via phone."
                    },
                    new OpenAiRealtimeToolDto
                    {
                        Type = "function",
                        Name = OpenAiToolConstants.HandleThirdPartyDelayedDelivery,
                        Description = "Address delayed delivery issues for orders placed through third-party platforms."
                    },
                    new OpenAiRealtimeToolDto
                    {
                        Type = "function",
                        Name = OpenAiToolConstants.HandleThirdPartyPickupTimeChange,
                        Description = "Manage pickup time modification requests for orders placed through third-party platforms."
                    },
                    new OpenAiRealtimeToolDto
                    {
                        Type = "function",
                        Name = OpenAiToolConstants.HandleThirdPartyFoodQuality,
                        Description = "Resolve food quality or taste complaints for orders placed through third-party platforms."
                    },
                    new OpenAiRealtimeToolDto
                    {
                        Type = "function",
                        Name = OpenAiToolConstants.HandleThirdPartyUnexpectedIssues,
                        Description = "Handle undefined or unexpected issues with orders placed through third-party platforms."
                    },
                    new OpenAiRealtimeToolDto
                    {
                        Type = "function",
                        Name = OpenAiToolConstants.HandlePromotionCalls,
                        Description = "Handles calls not related to the restaurant related to advertising, promotions, insurance or product marketing."
                    },
                    new OpenAiRealtimeToolDto
                    {
                        Type = "function",
                        Name = OpenAiToolConstants.CheckOrderStatus,
                        Description = "Check the status of a customer's order, including whether it is prepared and ready for pickup or delivery."
                    },
                    new OpenAiRealtimeToolDto
                    {
                        Type = "function",
                        Name = OpenAiToolConstants.RequestOrderDelivery,
                        Description = "When customers request delivery of their orders"
                    },
                    new OpenAiRealtimeToolDto
                    {
                        Type = "function",
                        Name = OpenAiToolConstants.Hangup,
                        Description = "When the customer says goodbye or something similar, hang up the phone"
                    },
                    new OpenAiRealtimeToolDto
                    {
                        Type = "function",
                        Name = OpenAiToolConstants.ConfirmOrder,
                        Description = "When the customer says that's enough, or clearly says he wants to place an order, the rest are not the final order, but just recording the order.",
                        Parameters = new OpenAiRealtimeToolParametersDto
                        {
                            Type = "object",
                            Properties = new
                            {
                                ordered_items = new
                                {
                                    type = "array",
                                    description = "List of items ordered by the customer",
                                    items = new
                                    {
                                        type = "object",
                                        properties = new
                                        {
                                            item_name = new
                                            {
                                                type = "string",
                                                description = "Name of the item ordered"
                                            },
                                            count = new
                                            {
                                                type = "number",
                                                description = "Quantity of the item ordered"
                                            },
                                            price = new
                                            {
                                                type = "string",
                                                description = "The price of the item multiplied by the quantity"
                                            },
                                            comment = new
                                            {
                                                type = "string",
                                                description = "Special requirements or comments regarding the item"
                                            },
                                            specification = new
                                            {
                                                type = "string",
                                                description = "Specified item size, such as large, medium, and small"
                                            }
                                        }
                                    }
                                },
                                total_price = new
                                {
                                    type = "number",
                                    description = "The total price of the customer order",
                                }
                            }
                        }
                    },
                }
            }
        };

        await SendToWebSocketAsync(openAiWebSocket, sessionUpdate);
    }
}