using System.Text;
using OpenAI.Interfaces;
using SmartTalk.Messages.Dto.WebSocket;

namespace SmartTalk.Core.Services.WebSocket;

public partial class Asterisk
{
    private async Task HandleRecordingFinishedEvent(RecordingFinishedEventDto recordingFinishedEvent, HttpClient client, string AriUrl, IOpenAIService openAiService)
    {
        switch (recordingFinishedEvent?.Type)
        {
            case "Dial":
                await HandleDialEvent(recordingFinishedEvent, client, AriUrl);
                break;
            case "StasisEnd":
                HandleStasisEndEvent();
                break;
            case "StasisStart":
                await HandleStasisStartEvent(client, AriUrl, openAiService);
                break;
            case "RecordingFinished":
                await HandleRecordingFinished(recordingFinishedEvent, client, AriUrl, openAiService);
                break;
        }
    }
    
    private async Task HandleDialEvent(RecordingFinishedEventDto recordingFinishedEvent, HttpClient client, string AriUrl)
    {
        OrderedFoods = new List<Food>();
        SessionId = Guid.NewGuid().ToString();
        ShoppingCart = new List<FoodDetailDto>();
        if (recordingFinishedEvent.dialstring == "801")
        {
            var responseMessage = await client.GetAsync($"{AriUrl}/channels");
            var channels = await responseMessage.Content.ReadAsAsync<List<AsteriskChannels>>();
        
            await MixChannelsIntoBridgeAsync(client, AriUrl, BridgeId, channels
                .Where(x => !x.Name.StartsWith("Snoop"))
                .Select(x => x.Id).ToList());
        }
    }
    
    private void HandleStasisEndEvent()
    {
        IsDial = false;
        IsEnd = true;
        _chatCount = 0;
        IsTurnToHuman = false;
        dialog = new StringBuilder();
        _count = 1;
        BridgeId = Guid.NewGuid().ToString();
        ShoppingCart = new List<FoodDetailDto>();
        SessionId = Guid.NewGuid().ToString();
        OrderedFoods = new List<Food>();
    }
    
    private async Task HandleStasisStartEvent(HttpClient client, string AriUrl, IOpenAIService openAiService)
    {
        if (!IsDial)
        {
            IsDial = true;
            IsEnd = false;
            await CreateSnoopChannelAsync(client, AriUrl, "6001", "100", true);
            await StartChannelRecordingAsync(client, AriUrl);
            await StartChannelAllRecordingAsync(client, AriUrl);
            await StartAskOpenAiAsync(openAiService);
        }
    }
    
    private async Task HandleRecordingFinished(RecordingFinishedEventDto recordingFinishedEvent, HttpClient client, string AriUrl, IOpenAIService openAiService)
    {
        if (!string.IsNullOrEmpty(AllSnoopRecordingName) && recordingFinishedEvent?.recording?.name == AllSnoopRecordingName)
        {
            var file = await GetAllRecordAsync(client, AriUrl).ConfigureAwait(false);
            var fileUrl = await UploadFileAsync(file).ConfigureAwait(false);
            await GetOrAddPhoneOrderAsync(url: fileUrl).ConfigureAwait(false);
            AllSnoopRecordingName = null;
        }
        
        if (!IsTurnToHuman && !IsEnd)
        {
            Console.WriteLine("current channel id: " + recordingFinishedEvent?.channel?.Id + ",single channel id: " + PartialSnoopChannelId + ", all channel id" + AllSnoopChannelId);
            Console.WriteLine($"is end: {IsEnd}");
        
            if (recordingFinishedEvent.recording.duration > 1)
            {
                if (recordingFinishedEvent.recording.name != lastRecordingName)
                {
                    Console.WriteLine("recording file name:" + recordingFinishedEvent.recording.name + "lastRecordingName:" + lastRecordingName);
                    lastRecordingName = recordingFinishedEvent.recording.name;
                    await ProcessRecordingAsync(client, openAiService, recordingFinishedEvent).ConfigureAwait(false);
                }
            }
            else
            {
                await DeleteRecordingAsync(client, AriUrl, recordingFinishedEvent.recording.name);
            }
        
            if (!IsTurnToHuman)
            {
                var responseMessage = await client.GetAsync($"{AriUrl}/channels");
                var channels = await responseMessage.Content.ReadAsAsync<List<AsteriskChannels>>();
                if (channels.Select(x => x.Id).Contains(_aiChannel))
                {
                    await StartRecordingAsync(client, AriUrl, _aiChannel, _count).ConfigureAwait(false);
                }
                _count += 1;
            }
        }
    }
}