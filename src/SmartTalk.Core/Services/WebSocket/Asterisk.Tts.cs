using SmartTalk.Messages.Dto.WebSocket;

namespace SmartTalk.Core.Services.WebSocket;

public partial class Asterisk
{
    private async Task<string> TtsAsync(HttpClient client, string aiReply)
    {
        client.DefaultRequestHeaders.Add("X-API-KEY", "xxx");
        
        var message = await client.PostAsJsonAsync("https://speech-test.sjdistributor.com/api/speech/tts", new TextToSpeechDto
        {
            Text = aiReply,
            SampleRate = 8000,
            VoiceId = 314
        });
                    
        var speechHttpResponseMessage = await message.Content.ReadAsAsync<SpeechResponseDto>();
        
        return speechHttpResponseMessage.Result;
    }
}