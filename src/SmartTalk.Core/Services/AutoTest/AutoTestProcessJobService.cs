using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.AutoTest;

public interface IAutoTestProcessJobService : IScopedDependency
{
    Task HandleTestingSpeechMaticsCallBackAsync(string jobId, CancellationToken cancellationToken);
}

public class AutoTestProcessJobService : IAutoTestProcessJobService
{
    private readonly IAutoTestDataProvider _autoTestDataProvider;

    public AutoTestProcessJobService(IAutoTestDataProvider autoTestDataProvider)
    {
        _autoTestDataProvider = autoTestDataProvider;
    }

    public async Task HandleTestingSpeechMaticsCallBackAsync(string jobId, CancellationToken cancellationToken)
    {
        var record = await _autoTestDataProvider.GetAutoTestTaskRecordBySpeechMaticsJobIdAsync(jobId, cancellationToken).ConfigureAwait(false);

        if (record == null) return;
    }
}