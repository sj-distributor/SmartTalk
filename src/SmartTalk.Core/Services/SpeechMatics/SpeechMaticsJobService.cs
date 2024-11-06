using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.SpeechMatics;

namespace SmartTalk.Core.Services.SpeechMatics;

public interface ISpeechMaticsJobService : IScopedDependency
{
    Task UploadSpeechMaticsKeysAsync(CancellationToken cancellationToken);
}

public class SpeechMaticsJobService : ISpeechMaticsJobService
{
    private readonly  ISpeechMaticsDataProvider _speechMaticsDataProvider;

    public SpeechMaticsJobService(ISpeechMaticsDataProvider speechMaticsDataProvider)
    {
        _speechMaticsDataProvider = speechMaticsDataProvider;
    }

    public async Task UploadSpeechMaticsKeysAsync(CancellationToken cancellationToken)
    {
        var currentDateTimeOffset = DateTimeOffset.UtcNow;
        
        var firstDayOfMonth = new DateTimeOffset(currentDateTimeOffset.Year, currentDateTimeOffset.Month, 1, 0, 0, 0, TimeSpan.Zero);
        
        var speechMaticsKeys = await _speechMaticsDataProvider.GetSpeechMaticsKeysAsync([SpeechMaticsKeyStatus.Discard], firstDayOfMonth, cancellationToken).ConfigureAwait(false);
        
        speechMaticsKeys.ForEach(x => { 
            x.Status = SpeechMaticsKeyStatus.NotEnabled;
            x.LastModifiedDate = null;
        });
        
        await _speechMaticsDataProvider.UpdateSpeechMaticsKeysAsync(speechMaticsKeys, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}