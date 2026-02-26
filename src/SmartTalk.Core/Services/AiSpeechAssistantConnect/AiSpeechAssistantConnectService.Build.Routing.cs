using Serilog;
using SmartTalk.Core.Domain.AISpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private async Task<string> ResolveInboundRouteAsync(CancellationToken cancellationToken)
    {
        var inboundRoute = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantInboundRouteAsync(_ctx.From, _ctx.To, cancellationToken).ConfigureAwait(false);

        var (forwardNumber, forwardAssistantId) = DecideDestinationByInboundRoute(inboundRoute);

        _ctx.ForwardAssistantId = forwardAssistantId;

        Log.Information("[AiAssistant] Route resolved, Routes: {RouteCount}, ForwardNumber: {ForwardNumber}, ForwardAssistantId: {ForwardAssistantId}, From: {From}, To: {To}", inboundRoute?.Count ?? 0, forwardNumber, forwardAssistantId, _ctx.From, _ctx.To);

        return forwardNumber;
    }

    private (string forwardNumber, int? forwardAssistantId) DecideDestinationByInboundRoute(List<AiSpeechAssistantInboundRoute> routes)
    {
        if (routes == null || routes.Count == 0)
            return (null, null);

        if (routes.Any(x => x.Emergency))
            routes = routes.Where(x => x.Emergency).ToList();

        foreach (var rule in routes)
        {
            var localNow = ConvertToRuleLocalTime(_clock.Now, rule.TimeZone);

            var days = ParseDays(rule.DayOfWeek) ?? [];
            var dayOk = days.Count == 0 || days.Contains(localNow.DayOfWeek);
            if (!dayOk) continue;

            var timeOk = rule.IsFullDay || IsWithinTimeWindow(localNow.TimeOfDay, rule.StartTime, rule.EndTime);
            if (!timeOk) continue;

            if (!string.IsNullOrWhiteSpace(rule.ForwardNumber))
                return (rule.ForwardNumber, null);

            if (rule.ForwardAssistantId.HasValue)
                return (null, rule.ForwardAssistantId.Value);
        }

        return (null, null);
    }

    private static DateTime ConvertToRuleLocalTime(DateTimeOffset utcNow, string timeZoneId)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(timeZoneId))
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                return TimeZoneInfo.ConvertTime(utcNow.UtcDateTime, tz);
            }
        }
        catch
        {
            return utcNow.UtcDateTime;
        }
        return utcNow.UtcDateTime;
    }

    private static List<DayOfWeek> ParseDays(string dayString)
    {
        if (string.IsNullOrWhiteSpace(dayString)) return [];

        var list = new List<DayOfWeek>();
        foreach (var token in dayString.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(token, out var v) && v is >= 0 and <= 6)
                list.Add((DayOfWeek)v);
        }
        return list;
    }

    private static bool IsWithinTimeWindow(TimeSpan localTime, TimeSpan? start, TimeSpan? end)
    {
        var startTime = start ?? TimeSpan.MinValue;
        var endTime = end ?? TimeSpan.MaxValue;

        if (startTime == endTime) return false;

        if (startTime < endTime) return localTime >= startTime && localTime <= endTime;

        return localTime >= startTime || localTime <= endTime;
    }
}
