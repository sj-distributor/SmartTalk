using OpenAI;
using System.Text;
using Newtonsoft.Json;
using OpenAI.Interfaces;
using OpenAI.Managers;
using OpenCCNET;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.WebSocket;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.WebSocket;

public interface IAsteriskService : IScopedDependency
{
    Task<List<string>> HandleIntentAsync(PhoneOrderIntent intent, IOpenAIService openAiService, HttpClient client, string question);
}

public partial class Asterisk : IAsteriskService
{
    private static int _count = 1;
    private static string _aiChannel;
    private const string AriUrl = "http://xxx:8088/ari";
    private const string AriUser = "admin";
    private const string AriPassword = "xxx@2024";
    private static string lastRecordingName;
    private static StringBuilder dialog = new();
    private static bool IsDial;
    private static bool IsTurnToHuman;
    private static string BridgeId = Guid.NewGuid().ToString("N");
    private static int _chatCount;
    private static PhoneOrderIntent _lastPhoneOrderIntent = PhoneOrderIntent.Default;
    private static bool IsEnd;
    private static List<FoodDetailDto> ShoppingCart = new();
    private static List<Food> OrderedFoods = new();
    private static string SessionId = Guid.NewGuid().ToString();
    private static string ReplyText = string.Empty;
    private static string PartialSnoopChannelId;
    private static string AllSnoopChannelId;
    private static string AllSnoopRecordingName;
    
    public void Run()
    {
        ZhConverter.Initialize();
        const string webSocketUrl = "ws://172.16.10.245:8088/ari/events?api_key=admin:xxx@2024&app=my_ari_app";
        
        var openAiService = new OpenAIService(new OpenAiOptions { ApiKey = "sk-xxx" });
        
        using var ws = new WebSocketSharp.WebSocket(webSocketUrl);

        ws.OnMessage += async (sender, e) =>
        {
            var client = CreateHttpClient();

            try
            {
                var recordingFinishedEvent = JsonConvert.DeserializeObject<RecordingFinishedEventDto>(e.Data);

                if (recordingFinishedEvent != null)
                    await HandleRecordingFinishedEvent(recordingFinishedEvent, client, AriUrl, openAiService);
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
            
            Console.WriteLine("Received: " + e.Data);
        };
        
        ws.OnError += (sender, e) =>
        {
            Console.WriteLine("Error: " + e.Message);
        };

        ws.OnOpen += (sender, e) =>
        {
            Console.WriteLine("WebSocket connection opened.");
        };

        ws.OnClose += (sender, e) =>
        {
            Console.WriteLine("WebSocket connection closed.");
        };
        
        ws.Connect();

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey(true);
    }
}