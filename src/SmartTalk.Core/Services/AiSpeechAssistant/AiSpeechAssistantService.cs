using Serilog;
using System.Text;
using Twilio.TwiML;
using System.Text.Json;
using SmartTalk.Core.Ioc;
using Twilio.AspNet.Core;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using Smarties.Messages.Extensions;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Enums.OpenAi;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.OpenAi;
using SmartTalk.Messages.Events.AiSpeechAssistant;
using Twilio.TwiML.Voice;
using Twilio.Types;
using Stream = Twilio.TwiML.Voice.Stream;
using Task = System.Threading.Tasks.Task;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public interface IAiSpeechAssistantService : IScopedDependency
{
    CallAiSpeechAssistantResponse CallAiSpeechAssistant(CallAiSpeechAssistantCommand command);

    Task<AiSpeechAssistantConnectCloseEvent> ConnectAiSpeechAssistantAsync(ConnectAiSpeechAssistantCommand command, CancellationToken cancellationToken);
}

public class AiSpeechAssistantService : IAiSpeechAssistantService
{
    private readonly OpenAiSettings _openAiSettings;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    public AiSpeechAssistantService(OpenAiSettings openAiSettings, IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _openAiSettings = openAiSettings;
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

        var knowledgeBase = await BuildingAiSpeechAssistantKnowledgeBaseAsync(command.From, command.To, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(knowledgeBase)) return new AiSpeechAssistantConnectCloseEvent();

        var openaiWebSocket = await ConnectOpenAiRealTimeSocketAsync(knowledgeBase, cancellationToken).ConfigureAwait(false);
        
        var context = new AiSpeechAssistantStreamContxtDto();
        
        var receiveFromTwilioTask = ReceiveFromTwilioAsync(command.TwilioWebSocket, openaiWebSocket, context);
        var sendToTwilioTask = SendToTwilioAsync(command.TwilioWebSocket, openaiWebSocket, context);

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

    private async Task<string> BuildingAiSpeechAssistantKnowledgeBaseAsync(string from, string to, CancellationToken cancellationToken)
    {
        var (assistant, promptTemplate, userProfile) = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantInfoByNumbersAsync(from, to, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Matching Ai speech assistant: {@Assistant}、{@PromptTemplate}、{@UserProfile}", assistant, promptTemplate, userProfile);

        if (assistant == null || promptTemplate == null || string.IsNullOrEmpty(promptTemplate.Template)) return string.Empty;

        var pstTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        var currentTime = pstTime.ToString("yyyy-MM-dd HH:mm:ss");
        
        var finalPrompt = promptTemplate.Template
            .Replace("#{user_profile}", string.IsNullOrEmpty(userProfile?.ProfileJson) ? $"CallerNumber:{from.TrimStart('+')}" : userProfile.ProfileJson)
            .Replace("#{current_time}", currentTime);
        
        Log.Information($"The final prompt: {finalPrompt}");

        return finalPrompt;
    }

    private async Task<WebSocket> ConnectOpenAiRealTimeSocketAsync(string prompt, CancellationToken cancellationToken)
    {
        var openAiWebSocket = new ClientWebSocket();
        openAiWebSocket.Options.SetRequestHeader("Authorization", $"Bearer {_openAiSettings.ApiKey}");
        openAiWebSocket.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");

        await openAiWebSocket.ConnectAsync(new Uri($"wss://api.openai.com/v1/realtime?model={OpenAiRealtimeModel.Gpt4o1217.GetDescription()}"), cancellationToken).ConfigureAwait(false);
        await SendSessionUpdateAsync(openAiWebSocket, prompt).ConfigureAwait(false);
        return openAiWebSocket;
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
                            context.StreamSid = jsonDocument?.RootElement.GetProperty("start").GetProperty("streamSid").GetString();
                            context.ResponseStartTimestampTwilio = null;
                            context.LatestMediaTimestamp = 0;
                            context.LastAssistantItem = null;
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

    private async Task SendToTwilioAsync(WebSocket twilioWebSocket, WebSocket openAiWebSocket, AiSpeechAssistantStreamContxtDto context)
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
        await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message))), WebSocketMessageType.Text, true, CancellationToken.None);
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
                        Name = "record_customer_info",
                        Description = "Record the customer's name and phone number.",
                        Parameters = new OpenAiRealtimeToolParametersDto
                        {
                            Type = "object",
                            Properties = new
                            {
                                customer_name = new
                                {
                                    type = "string",
                                    description = "Name of the customer"
                                },
                                customer_phone = new
                                {
                                    type = "string",
                                    description = "Phone number of the customer"
                                }
                            }
                        }
                    },
                    new OpenAiRealtimeToolDto
                    {
                        Type = "function",
                        Name = "update_order",
                        Description = "When the customer modifies the items in the current order, such as adding or reducing items or modifying the notes or specifications of the items, a new order is updated based on the current order.",
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
                        Name = "repeat_order",
                        Description = "The customer needs to repeat the order."
                    },
                    new OpenAiRealtimeToolDto
                    {
                        Type = "function",
                        Name = "order",
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