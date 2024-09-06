using System.Text;
using Newtonsoft.Json;
using SmartTalk.Messages.Dto.WebSocket;

namespace SmartTalk.Core.Services.WebSocket;

public partial class Asterisk
{
    private async Task CreateBridgeAsync(HttpClient client, string ariUrl, string bridgeId)
    {
        var content = new StringContent(JsonConvert.SerializeObject(new
        {
            type = "mixing",
            name = "myBridge"
        }), Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"{ariUrl}/bridges/{bridgeId}", content);
        response.EnsureSuccessStatusCode();
    }
    
    private async Task MixChannelsIntoBridgeAsync(HttpClient client, string ariUrl, string bridgeId, List<string> channelIds)
    {
        var content = new StringContent(JsonConvert.SerializeObject(new
        {
            channel = channelIds
        }), Encoding.UTF8, "application/json");
        
        var response = await client.PostAsync($"{ariUrl}/bridges/{bridgeId}/addChannel", content);
        response.EnsureSuccessStatusCode();
    }
    
    private async Task CreateSnoopChannelAsync(HttpClient client, string ariUrl, string extension, string callerId, bool isSpy)
    {
        var channel = new
        {
            endpoint = $"PJSIP/{extension}",
            app = "my_ari_app",
            callerId = callerId
        };

        var channelContent = new StringContent(JsonConvert.SerializeObject(channel), Encoding.UTF8, "application/json");
        var channelResponse = await client.PostAsync($"{ariUrl}/channels", channelContent);
        channelResponse.EnsureSuccessStatusCode();
        
        await Task.Delay(1000);
        
        var responseMessage = await client.GetAsync($"{ariUrl}/channels");
        var speechHttpResponseMessage = await responseMessage.Content.ReadAsAsync<List<AsteriskChannels>>();
        
        if (isSpy)
        {
            _aiChannel = speechHttpResponseMessage.First().Id;
        
            var snoopChannel = new
            {
                spy = "in",
                app = "my_ari_app"
                // snoopId = Guid.NewGuid()
            };
        
            var snoopContent = new StringContent(JsonConvert.SerializeObject(snoopChannel), Encoding.UTF8, "application/json");
            var snoopChannelResponse = await client.PostAsync($"{ariUrl}/channels/{_aiChannel}/snoop", snoopContent);
            var snoop = await snoopChannelResponse.Content.ReadAsAsync<AsteriskChannels>();
            PartialSnoopChannelId = snoop.Id;
            snoopChannelResponse.EnsureSuccessStatusCode();
        }
        
        var snoopAllChannel = new
        {
            spy = "both",
            app = "my_ari_app"
        };
        
        var snoopAllContent = new StringContent(JsonConvert.SerializeObject(snoopAllChannel), Encoding.UTF8, "application/json");
        var snoopAllChannelResponse = await client.PostAsync($"{ariUrl}/channels/{_aiChannel}/snoop", snoopAllContent);
        var allSnoop = await snoopAllChannelResponse.Content.ReadAsAsync<AsteriskChannels>();
        AllSnoopChannelId = allSnoop.Id;
        // await PlayAudioAsync(client, AriUrl, _aiChannel, "https://speech-test.sjdistributor.com/data/tts/dcf03a63-0916-4672-8fad-880b80284989.wav");
    }
    
    private async Task PlayAudioAsync(HttpClient client, string ariUrl, string channelId, List<string> audioUrls)
    {
        foreach (var audioUrl in audioUrls)
        {
            await client.PostAsync($"{ariUrl}/channels/{channelId}/play?media=sound:{audioUrl}", null);
        }
    }
    
    private async Task TurnToManualCustomerServiceAsync(HttpClient client, string ariUrl, string channelId)
    {
        await CreateBridgeAsync(client, AriUrl, BridgeId);
        
        await CreateSnoopChannelAsync(client, AriUrl, "801", "100", false);
        
        Console.WriteLine("start dial to 801");
    }
}