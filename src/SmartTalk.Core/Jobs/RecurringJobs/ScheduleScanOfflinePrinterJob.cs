using Mediator.Net;
using SmartTalk.Core.Settings.Printer;
using SmartTalk.Messages.Commands.Printer;

namespace SmartTalk.Core.Jobs.RecurringJobs;

public class ScheduleScanOfflinePrinterJob : IRecurringJob
{
    private readonly IMediator _mediator;
    private readonly ScheduleScanOfflinePrinterJobExpressionSetting _setting;
    
    public ScheduleScanOfflinePrinterJob(IMediator mediator, ScheduleScanOfflinePrinterJobExpressionSetting setting)
    {
        _mediator = mediator;
        _setting = setting;
    }
    
    public async Task Execute()
    {
        await _mediator.SendAsync(new ScanOfflinePrinterCommand()).ConfigureAwait(false);   
    }

    public string JobId => nameof(ScheduleScanOfflinePrinterJob);
    public string CronExpression => _setting.CronExpression;
}