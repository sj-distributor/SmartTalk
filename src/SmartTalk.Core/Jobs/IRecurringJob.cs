namespace SmartTalk.Core.Jobs;

public interface IRecurringJob : IJob
{
    string CronExpression { get; }

    TimeZoneInfo TimeZone => null;
}