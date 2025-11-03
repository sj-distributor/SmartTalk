using Newtonsoft.Json;
using Serilog;
using SmartTalk.Messages.Dto.Asr;
using SmartTalk.Messages.Enums.HrInterView;
using SmartTalk.Messages.Events.HrInterView;

namespace SmartTalk.Core.Services.EventHandling;

public partial class EventHandlingService
{
    public async Task HandlingEventAsync(ConnectWebSocketEvent @event, CancellationToken cancellationToken)
    {
        var sessions = await _hrInterViewDataProvider.GetHrInterViewSessionsBySessionIdAsync(@event.SessionId, cancellationToken).ConfigureAwait(false);
        
        Log.Information("HandlingEventAsync ConnectWebSocketEvent sessions:{@sessions}",sessions);
        
        foreach (var session in sessions.Where(x => x.QuestionType == HrInterViewSessionQuestionType.User))
        {
            Log.Information("HandlingEventAsync ConnectWebSocketEvent before fileUrl:{@fileUrl}",session.FileUrl);
            
            var fileUrl = JsonConvert.DeserializeObject<List<string>>(session.FileUrl).FirstOrDefault();
            
            Log.Information("HandlingEventAsync ConnectWebSocketEvent fileUrl:{@fileUrl}",fileUrl);
            
            var bytes = await _httpClientFactory.GetAsync<byte[]>(fileUrl, cancellationToken).ConfigureAwait(false);

            if (IsWavFile(bytes)) continue;
            
            var answers = await _asrClient.TranscriptionAsync(new AsrTranscriptionDto { File = bytes }, cancellationToken).ConfigureAwait(false);
            
            session.Message = answers.Text;
        }
        
        await _hrInterViewDataProvider.UpdateHrInterViewSessionsAsync(sessions, cancellationToken:cancellationToken).ConfigureAwait(false);
        
       var setting = await _hrInterViewDataProvider.GetHrInterViewSettingBySessionIdAsync(@event.SessionId, cancellationToken).ConfigureAwait(false);

       setting.IsConvertText = true;
       
       await _hrInterViewDataProvider.UpdateHrInterViewSettingAsync(setting, cancellationToken:cancellationToken).ConfigureAwait(false);
    }
    
    public static bool IsWavFile(byte[] data)
    {
        if (data == null || data.Length < 12)
            return false;
        
        if (data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F')
            return false;
        
        if (data[8] != 'W' || data[9] != 'A' || data[10] != 'V' || data[11] != 'E')
            return false;

        return true;
    }
}