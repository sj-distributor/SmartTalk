using Newtonsoft.Json;
using SmartTalk.Messages.Dto.Asr;
using SmartTalk.Messages.Enums.HrInterView;
using SmartTalk.Messages.Events.HrInterView;

namespace SmartTalk.Core.Services.EventHandling;

public partial class EventHandlingService
{
    public async Task HandlingEventAsync(ConnectWebSocketEvent @event, CancellationToken cancellationToken)
    {
        var sessions = await _hrInterViewDataProvider.GetHrInterViewSessionsBySessionIdAsync(@event.SessionId, cancellationToken).ConfigureAwait(false);
        
        foreach (var session in sessions.Where(x => x.QuestionType == HrInterViewSessionQuestionType.User))
        {
            var bytes = await _httpClientFactory.GetAsync<byte[]>(JsonConvert.DeserializeObject<List<string>>(session.FileUrl).FirstOrDefault(), cancellationToken:cancellationToken).ConfigureAwait(false);
            var answers = await _asrClient.TranscriptionAsync(new AsrTranscriptionDto { File = bytes }, cancellationToken).ConfigureAwait(false);
            session.Message = answers.Text;
        }
        
        await _hrInterViewDataProvider.UpdateHrInterViewSessionsAsync(sessions, cancellationToken:cancellationToken).ConfigureAwait(false);
        
       var setting = await _hrInterViewDataProvider.GetHrInterViewSettingBySessionIdAsync(@event.SessionId, cancellationToken).ConfigureAwait(false);

       setting.IsConvertText = true;
       
       await _hrInterViewDataProvider.UpdateHrInterViewSettingAsync(setting, cancellationToken:cancellationToken).ConfigureAwait(false);
    }
}