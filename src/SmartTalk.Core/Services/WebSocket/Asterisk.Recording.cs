using System.Text;
using Newtonsoft.Json;
using OpenAI.Interfaces;
using SmartTalk.Messages.Dto.WebSocket;

namespace SmartTalk.Core.Services.WebSocket;

public partial class Asterisk
{
    private async Task StartChannelRecordingAsync(HttpClient client, string ariUrl)
    {
        try
        {
            AllSnoopRecordingName = "my_snoop_recording_" + Guid.NewGuid();
            // 开始录音
            var record = new
            {
                name = AllSnoopRecordingName,
                format = "wav",
                maxDurationSeconds = 3600,
                maxSilenceSeconds = 1,
                ifExists = "overwrite",
                beep = false,
                terminateOn = "none"
            };

            var recordContent = new StringContent(JsonConvert.SerializeObject(record), Encoding.UTF8, "application/json");
            var recordResponse = await client.PostAsync($"{ariUrl}/channels/{_aiChannel}/record", recordContent);

            if (recordResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("录音开始。" + AllSnoopRecordingName);
            }
            else
            {
                Console.WriteLine($"录音失败：{recordResponse.ReasonPhrase}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"发生错误：{ex.Message}");
        }
    }
    
    private static async Task<byte[]> GetAllRecordAsync(HttpClient client, string ariUrl)
    {
        var recordResponse = await client.GetAsync($"{ariUrl}/recordings/stored/{AllSnoopRecordingName}/file");

        return await recordResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
    }
    
    private async Task<byte[]> FetchRecordingFileAsync(HttpClient client, string ariUrl, string fileName)
    {
        var recordResponse = await client.GetAsync($"{ariUrl}/recordings/stored/{fileName}/file");
        return await recordResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
    }
    
    private async Task ProcessRecordingAsync(HttpClient client, IOpenAIService openAiService, RecordingFinishedEventDto recordingFinishedEvent)
    {
        var file = await FetchRecordingFileAsync(client, AriUrl, recordingFinishedEvent.recording.name).ConfigureAwait(false);
        
        var transcription = await TranscriptRecordingAsync(openAiService, file, recordingFinishedEvent.recording.name).ConfigureAwait(false);
        if (string.IsNullOrEmpty(transcription.Text) || string.IsNullOrWhiteSpace(transcription.Text) || transcription.Text == "以上言论不代表本台立场" || transcription.Text.Contains("字幕") || transcription.Text.Contains("点赞 订阅 转发 打赏") || transcription.Text.Contains("多谢您收睇时局新闻")) { await DeleteRecordingAsync(client, AriUrl, recordingFinishedEvent.recording.name); return; }
        Console.WriteLine("Transcription:" + transcription.Text);
        dialog.AppendLine("user question:" + transcription.Text);
        
        var intent = await RecognizeIntentAsync(openAiService, transcription.Text).ConfigureAwait(false);
        var speech = await HandleIntentAsync(intent, openAiService, client, transcription.Text).ConfigureAwait(false);
        Console.WriteLine("Reply speech:" + speech);
        
        await PlayAudioAsync(client, AriUrl, _aiChannel, speech);

        try
        {
            if (!string.IsNullOrEmpty(transcription?.Text)) await GetOrAddPhoneOrderAsync(transcription.Text, ReplyText).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Console.WriteLine("store data error" + e.Message);
        }
    }
    
    private async Task StartChannelAllRecordingAsync(HttpClient client, string ariUrl)
    {
        try
        {
            var myAllSnoopRecording = "my_all_snoop_recording_" + Guid.NewGuid();
            var record = new
            {
                name = myAllSnoopRecording,
                format = "wav",
                ifExists = "overwrite",
                beep = false,
                terminateOn = "none"
            };

            var recordContent = new StringContent(JsonConvert.SerializeObject(record), Encoding.UTF8, "application/json");
            var recordResponse = await client.PostAsync($"{ariUrl}/channels/{AllSnoopChannelId}/record", recordContent);

            if (recordResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("录音开始。" + myAllSnoopRecording);
            }
            else
            {
                Console.WriteLine($"录音失败：{recordResponse.ReasonPhrase}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"发生错误：{ex.Message}");
        }
    }
    
    private async Task StartRecordingAsync(HttpClient client, string ariUrl, string snoopChannelId, int count)
    {
        try
        {
            // 开始录音
            var record = new
            {
                name = "my_recording_v2_" + count,
                format = "wav",
                maxDurationSeconds = 3600,
                maxSilenceSeconds = 1,
                ifExists = "overwrite",
                beep = false,
                terminateOn = "none" 
            };

            var recordContent = new StringContent(JsonConvert.SerializeObject(record), Encoding.UTF8, "application/json");
            var recordResponse = await client.PostAsync($"{AriUrl}/channels/{snoopChannelId}/record", recordContent);

            // await Task.Delay(500);
            Console.WriteLine(recordResponse.IsSuccessStatusCode ? "录音开始。" : $"录音失败：{recordResponse.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"发生错误：{ex.Message}");
        }
    }
    
    private async Task DeleteRecordingAsync(HttpClient client, string ariUrl, string recordingName)
    {
        await client.PostAsync($"{ariUrl}/recordings/stored/{recordingName}", null);
    }
}