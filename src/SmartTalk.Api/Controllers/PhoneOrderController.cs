using System.Net.WebSockets;
using System.Text;
using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PhoneOrderController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly string? _openAiApiKey;

    public PhoneOrderController(IMediator mediator, IConfiguration configuration)
    {
        _mediator = mediator;
        _openAiApiKey = configuration["OpenAI:ApiKey"];
    }
    
    [Route("records"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneOrderRecordsResponse))]
    public async Task<IActionResult> GetPhoneOrderRecordsAsync([FromQuery] GetPhoneOrderRecordsRequest request)
    {
        var response = await _mediator.RequestAsync<GetPhoneOrderRecordsRequest, GetPhoneOrderRecordsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("conversations"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneOrderConversationsResponse))]
    public async Task<IActionResult> GetPhoneOrderConversationsAsync([FromQuery] GetPhoneOrderConversationsRequest request)
    {
        var response = await _mediator.RequestAsync<GetPhoneOrderConversationsRequest, GetPhoneOrderConversationsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("items"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneOrderOrderItemsRessponse))]
    public async Task<IActionResult> GetPhoneOrderOrderItemsAsync([FromQuery] GetPhoneOrderOrderItemsRequest request)
    {
        var response = await _mediator.RequestAsync<GetPhoneOrderOrderItemsRequest, GetPhoneOrderOrderItemsRessponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("conversation/add"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AddPhoneOrderConversationsResponse))]
    public async Task<IActionResult> AddPhoneOrderConversationsAsync([FromBody] AddPhoneOrderConversationsCommand command) 
    {
        var response = await _mediator.SendAsync<AddPhoneOrderConversationsCommand, AddPhoneOrderConversationsResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }

    [HttpPost("record/receive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReceivePhoneOrderRecordAsync([FromForm] IFormFile file, [FromForm] string restaurant)
    {
        var ms = new MemoryStream();

        await file.CopyToAsync(ms).ConfigureAwait(false);

        var fileContent = ms.ToArray();
        
        await _mediator.SendAsync(new ReceivePhoneOrderRecordCommand { RecordName = file.FileName, RecordContent = fileContent, Restaurant = restaurant}).ConfigureAwait(false);
        
        return Ok();
    }

    [HttpPost("transcription/callback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TranscriptionCallbackAsync(JObject jObject)
    {
        Log.Information("Receive parameter : {jObject}", jObject.ToString());
        
        var transcription = jObject.ToObject<SpeechMaticsGetTranscriptionResponseDto>();
        
        Log.Information("Transcription : {@transcription}", transcription);
        
        await _mediator.SendAsync(new HandleTranscriptionCallbackCommand { Transcription = transcription }).ConfigureAwait(false);

        return Ok();
    }

    [Route("manual/order"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AddOrUpdateManualOrderResponse))]
    public async Task<IActionResult> AddOrUpdateManualOrderAsync([FromBody] AddOrUpdateManualOrderCommand command)
    {
        var response = await _mediator.SendAsync<AddOrUpdateManualOrderCommand, AddOrUpdateManualOrderResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [HttpPost("incoming-call")]
    public async Task<IActionResult> HandleIncomingCallAsync()
    {
        var host = HttpContext.Request.Host.Host;
        
        var twimlResponse = $@"
            <Response>
                <Connect>
                    <Stream url='wss://{host}/call/media-stream' />
                </Connect>
            </Response>";

        return Ok(Content(twimlResponse, "application/xml"));
    }
    
    [HttpGet("media-stream")]
    public async Task HandleMediaStreamAsync()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var clientSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            Console.WriteLine("Client connected");

            using var openaiSocket = new ClientWebSocket();
            openaiSocket.Options.SetRequestHeader("Authorization", $"Bearer {_openAiApiKey}");
            openaiSocket.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");

            var openaiUri = new Uri("wss://api.openai.com/v1/realtime?model=gpt-4o-realtime-preview-2024-10-01");
            await openaiSocket.ConnectAsync(openaiUri, CancellationToken.None);

            await SendSessionUpdate(openaiSocket);
            await ProxyWebSocketTraffic(clientSocket, openaiSocket);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }

    private async Task SendSessionUpdate(WebSocket openaiSocket)
    {
        var sessionUpdate = new
        {
            type = "session_update",
            data = new { }
        };

        var json = JsonConvert.SerializeObject(sessionUpdate);
        var buffer = Encoding.UTF8.GetBytes(json);

        await openaiSocket.SendAsync(
            new ArraySegment<byte>(buffer),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
    }

    private async Task ProxyWebSocketTraffic(WebSocket clientSocket, WebSocket openaiSocket)
    {
        var buffer = new byte[1024 * 4];

        async Task HandleSocket(WebSocket sourceSocket, WebSocket destinationSocket)
        {
            while (sourceSocket.State == WebSocketState.Open)
            {
                var result = await sourceSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await destinationSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None);
                    break;
                }

                await destinationSocket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, result.Count),
                    result.MessageType,
                    result.EndOfMessage,
                    CancellationToken.None);
            }
        }

        var clientToOpenAITask = HandleSocket(clientSocket, openaiSocket);
        var openaiToClientTask = HandleSocket(openaiSocket, clientSocket);

        await Task.WhenAll(clientToOpenAITask, openaiToClientTask);
    }
}