using Serilog;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Smarties;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private async Task<string> BuildKnowledgeBaseAsync(
        SessionBusinessContext ctx, string from, string to,
        int? assistantId, int? numberId, int? agentId,
        CancellationToken cancellationToken)
    {
        var inboundRoute = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantInboundRouteAsync(from, to, cancellationToken).ConfigureAwait(false);

        Log.Information("Inbound route: {@inboundRoute}", inboundRoute);

        var (forwardNumber, forwardAssistantId) = DecideDestinationByInboundRoute(inboundRoute);

        Log.Information("Forward number: {@forwardNumber} or Forward assistant id: {forwardAssistantId}", forwardNumber, forwardAssistantId);

        if (!string.IsNullOrEmpty(forwardNumber))
        {
            ctx.ShouldForward = true;
            ctx.ForwardPhoneNumber = forwardNumber;
            return null;
        }

        var (assistant, knowledge, userProfile) = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantInfoByNumbersAsync(from, to, forwardAssistantId ?? assistantId, cancellationToken).ConfigureAwait(false);

        Log.Information("Matching Ai speech assistant: {@Assistant}, {@Knowledge}, {@UserProfile}", assistant, knowledge, userProfile);

        var pstTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
        var currentTime = pstTime.ToString("yyyy-MM-dd HH:mm:ss");

        var finalPrompt = knowledge.Prompt
            .Replace("#{user_profile}", string.IsNullOrEmpty(userProfile?.ProfileJson) ? " " : userProfile.ProfileJson)
            .Replace("#{current_time}", currentTime)
            .Replace("#{customer_phone}", from.StartsWith("+1") ? from[2..] : from)
            .Replace("#{pst_date}", $"{pstTime.Date:yyyy-MM-dd} {pstTime.DayOfWeek}");

        if (numberId.HasValue && finalPrompt.Contains("#{greeting}"))
        {
            var greeting = await _smartiesClient
                .GetSaleAutoCallNumberAsync(new GetSaleAutoCallNumberRequest { Id = numberId.Value }, cancellationToken).ConfigureAwait(false);
            knowledge.Greetings = string.IsNullOrEmpty(greeting.Data.Number.Greeting) ? knowledge.Greetings : greeting.Data.Number.Greeting;

            finalPrompt = finalPrompt.Replace("#{greeting}", knowledge.Greetings ?? string.Empty);
        }

        if (finalPrompt.Contains("#{customer_items}", StringComparison.OrdinalIgnoreCase))
        {
            var soldToIds = !string.IsNullOrEmpty(assistant.Name) ? assistant.Name.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList() : new List<string>();

            if (soldToIds.Any())
            {
                var caches = await _salesDataProvider.GetCustomerItemsCacheBySoldToIdsAsync(soldToIds, cancellationToken).ConfigureAwait(false);

                var customerItems = caches.Where(c => !string.IsNullOrEmpty(c.CacheValue)).Select(c => c.CacheValue.Trim()).Distinct().ToList();

                finalPrompt = finalPrompt.Replace("#{customer_items}", customerItems.Any() ? string.Join(Environment.NewLine + Environment.NewLine, customerItems.Take(50)) : " ");
            }
        }

        if (agentId.HasValue && finalPrompt.Contains("#{menu_items}", StringComparison.OrdinalIgnoreCase))
        {
            var menuItems = await GenerateMenuItemsAsync(agentId.Value, cancellationToken).ConfigureAwait(false);

            finalPrompt = finalPrompt.Replace("#{menu_items}", string.IsNullOrWhiteSpace(menuItems) ? "" : menuItems);
        }

        if (finalPrompt.Contains("#{customer_info}", StringComparison.OrdinalIgnoreCase))
        {
            var customerInfoCache = await _salesDataProvider.GetCustomerInfoCacheByPhoneNumberAsync(from, cancellationToken).ConfigureAwait(false);

            var info = customerInfoCache?.CacheValue?.Trim();

            finalPrompt = finalPrompt.Replace("#{customer_info}", string.IsNullOrEmpty(info) ? " " : info);
        }

        Log.Information("The final prompt: {FinalPrompt}", finalPrompt);

        ctx.Assistant = _mapper.Map<AiSpeechAssistantDto>(assistant);
        ctx.Knowledge = _mapper.Map<AiSpeechAssistantKnowledgeDto>(knowledge);
        ctx.ModelName = assistant.ModelName;

        return finalPrompt;
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
