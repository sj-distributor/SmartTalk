using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Serilog;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderProcessJobService
{
    Task HandleReleasedSpeechMaticsDiarizedTranscribeCallBackAsync(string jobId, CancellationToken cancellationToken);
}

public partial class PhoneOrderProcessJobService
{
    private static readonly HashSet<string> QuantityLeadWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty",
        "half", "quarter", "pair", "pairs", "dozen", "dozens", "case", "cases", "box", "boxes", "bag", "bags",
        "pack", "packs", "bottle", "bottles", "jar", "jars", "tray", "trays", "bucket", "buckets", "piece", "pieces",
        "cup", "cups", "can", "cans", "carton", "cartons", "pound", "pounds", "lb", "lbs", "kg", "kilogram", "kilograms"
    };

    private static readonly HashSet<string> QuantityUnitWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "case", "cases", "box", "boxes", "bag", "bags", "pack", "packs", "bottle", "bottles", "jar", "jars",
        "tray", "trays", "bucket", "buckets", "piece", "pieces", "cup", "cups", "can", "cans", "carton", "cartons",
        "pound", "pounds", "lb", "lbs", "kg", "kilogram", "kilograms", "oz", "ounce", "ounces"
    };

    private static readonly HashSet<string> FragmentLeadWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "or", "with", "without", "plus", "of", "for", "to", "then", "also"
    };

    public async Task HandleReleasedSpeechMaticsDiarizedTranscribeCallBackAsync(string jobId, CancellationToken cancellationToken)
    {
        if (jobId == null) return;

        var record = await _phoneOrderDataProvider.GetPhoneOrderRecordByTranscriptionJobIdAsync(jobId, cancellationToken).ConfigureAwait(false);

        Log.Information("Get Phone order record : {@record}", record);

        if (record == null) return;

        try
        {
            record.Status = PhoneOrderRecordStatus.Transcription;

            await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);

            var audioContent = await _smartTalkHttpClientFactory.GetAsync<byte[]>(record.Url, cancellationToken).ConfigureAwait(false);

            await SummarizeConversationContentAsync(record, audioContent, cancellationToken).ConfigureAwait(false);

            var speakInfos = await TranscribePhoneOrderSegmentsByDiarizedAsync(audioContent, cancellationToken).ConfigureAwait(false);
            var normalizedSpeakInfos = NormalizeDiarizedSpeakInfos(speakInfos);

            if (normalizedSpeakInfos.Count == 0)
            {
                Log.Warning("Diarized transcription generated no valid segments. RecordId: {RecordId}, JobId: {JobId}", record.Id, jobId);
            }

            await _phoneOrderService.ProcessPhoneOrderDiarizedTranscriptionAsync(normalizedSpeakInfos, record, cancellationToken).ConfigureAwait(false);

            await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, cancellationToken: cancellationToken).ConfigureAwait(false);

            _smartTalkBackgroundJobClient.Enqueue<IPhoneOrderProcessJobService>(x => x.CalculateRecordingDurationAsync(record, null, cancellationToken), HangfireConstants.InternalHostingFfmpeg);
        }
        catch (Exception e)
        {
            record.Status = PhoneOrderRecordStatus.Exception;

            await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken)
                .ConfigureAwait(false);

            Log.Warning("Handle transcription callback failed: {@Exception}", e);
        }
    }

    private List<PhoneOrderDiarizedSpeakInfoDto> NormalizeDiarizedSpeakInfos(List<PhoneOrderDiarizedSpeakInfoDto> speakInfos)
    {
        if (speakInfos is not { Count: > 0 })
            return [];

        var orderedSpeakInfos = speakInfos
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .Select(x => new PhoneOrderDiarizedSpeakInfoDto
            {
                StartTime = Math.Max(0, x.StartTime),
                EndTime = x.EndTime < x.StartTime ? x.StartTime : x.EndTime,
                Speaker = string.IsNullOrWhiteSpace(x.Speaker) ? "unknown" : x.Speaker.Trim(),
                Role = x.Role,
                RoleText = x.RoleText,
                Text = NormalizeSegmentText(x.Text)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .OrderBy(x => x.StartTime)
            .ThenBy(x => x.EndTime)
            .ToList();

        var distinctSpeakers = orderedSpeakInfos
            .Select(x => x.Speaker)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctSpeakers.Count > 2)
        {
            Log.Warning("Diarized transcription returned more than two speakers. Speakers: {@Speakers}", distinctSpeakers);
        }

        var restaurantSpeaker = DetermineRestaurantSpeaker(orderedSpeakInfos);

        Log.Information("Locked diarized restaurant speaker. RestaurantSpeaker: {RestaurantSpeaker}", restaurantSpeaker);

        foreach (var speakInfo in orderedSpeakInfos)
        {
            var originalSpeaker = speakInfo.Speaker;
            var isRestaurantSpeaker = string.Equals(originalSpeaker, restaurantSpeaker, StringComparison.OrdinalIgnoreCase);

            speakInfo.Speaker = isRestaurantSpeaker ? "S1" : "S2";
            speakInfo.Role = isRestaurantSpeaker ? PhoneOrderRole.Restaurant : PhoneOrderRole.Client;
        }

        for (var i = 1; i < orderedSpeakInfos.Count - 1; i++)
        {
            var previous = orderedSpeakInfos[i - 1];
            var current = orderedSpeakInfos[i];
            var next = orderedSpeakInfos[i + 1];

            if (previous.Speaker == next.Speaker &&
                current.Speaker != previous.Speaker &&
                ShouldSmoothIntermediateSegment(previous, current, next))
            {
                current.Speaker = previous.Speaker;
                current.Role = previous.Role;
            }
        }

        var mergedSpeakInfos = new List<PhoneOrderDiarizedSpeakInfoDto>();

        foreach (var speakInfo in orderedSpeakInfos)
        {
            if (mergedSpeakInfos.Count == 0)
            {
                mergedSpeakInfos.Add(speakInfo);
                continue;
            }

            var lastSpeakInfo = mergedSpeakInfos[^1];
            var gap = speakInfo.StartTime - lastSpeakInfo.EndTime;

            if (lastSpeakInfo.Speaker == speakInfo.Speaker && gap <= 0.8)
            {
                lastSpeakInfo.EndTime = Math.Max(lastSpeakInfo.EndTime, speakInfo.EndTime);
                lastSpeakInfo.Text = string.Join(" ", new[] { lastSpeakInfo.Text, speakInfo.Text }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
                continue;
            }

            mergedSpeakInfos.Add(speakInfo);
        }

        mergedSpeakInfos = StitchFragmentedDiarizedSegments(mergedSpeakInfos);

        Log.Information("Normalized diarized speak infos: {@SpeakInfos}", mergedSpeakInfos);

        return mergedSpeakInfos;
    }

    private async Task<List<PhoneOrderDiarizedSpeakInfoDto>> TranscribePhoneOrderSegmentsByDiarizedAsync(byte[] audioContent, CancellationToken cancellationToken)
    {
        Log.Information("Starting diarized transcription. FileSize: {FileSize}", audioContent?.Length ?? 0);

        var responseText = await _openaiClient.TranscribeDiarizedAudioAsync(audioContent, "recording.wav", cancellationToken).ConfigureAwait(false);

        Log.Information("Diarized transcription response received. BodyPreview: {BodyPreview}", BuildResponsePreview(responseText));

        if (string.IsNullOrWhiteSpace(responseText))
            return [];

        var payload = JObject.Parse(responseText);

        if (payload["error"] is JObject error)
            throw new InvalidOperationException($"Diarized transcription request failed. Body={BuildResponsePreview(error.ToString())}");

        var segments = payload["segments"] as JArray;

        if (segments == null || segments.Count == 0)
        {
            Log.Warning(
                "Diarized transcription returned no segments. PayloadPreview: {PayloadPreview}", BuildResponsePreview(responseText));
            return [];
        }

        var speakInfos = segments
            .OfType<JObject>()
            .Select(x => new PhoneOrderDiarizedSpeakInfoDto
            {
                StartTime = x.Value<double?>("start") ?? 0,
                EndTime = x.Value<double?>("end") ?? 0,
                Speaker = x.Value<string>("speaker") ?? string.Empty,
                Role = TryParseDiarizedRole(x.Value<string>("role"), out var role) ? role : null,
                RoleText = x.Value<string>("role")?.Trim() ?? string.Empty,
                Text = x.Value<string>("text")?.Trim() ?? string.Empty
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .ToList();

        Log.Information("Diarized transcription parsed segments: {@SpeakInfos}", speakInfos);

        return speakInfos;
    }

    private static string BuildResponsePreview(string responseText, int maxLength = 1200)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return "<empty>";

        var normalized = responseText.Replace("\r", " ").Replace("\n", " ").Trim();

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
    }

    private static bool ShouldSmoothIntermediateSegment(PhoneOrderDiarizedSpeakInfoDto previous, PhoneOrderDiarizedSpeakInfoDto current, PhoneOrderDiarizedSpeakInfoDto next)
    {
        var duration = current.EndTime - current.StartTime;
        var previousGap = current.StartTime - previous.EndTime;
        var nextGap = next.StartTime - current.EndTime;
        var wordCount = current.Text?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;

        return duration <= 1.2 && previousGap <= 0.6 && nextGap <= 0.6 && (wordCount <= 4 || (current.Text?.Length ?? 0) <= 24);
    }

    private static string DetermineRestaurantSpeaker(List<PhoneOrderDiarizedSpeakInfoDto> speakInfos)
    {
        if (speakInfos is not { Count: > 0 })
            return "unknown";

        var firstSpeaker = speakInfos[0].Speaker;
        var speakerScores = speakInfos
            .GroupBy(x => x.Speaker, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var restaurantDuration = group
                    .Where(x => x.Role == PhoneOrderRole.Restaurant)
                    .Sum(x => Math.Max(0.1, x.EndTime - x.StartTime));

                var clientDuration = group
                    .Where(x => x.Role == PhoneOrderRole.Client)
                    .Sum(x => Math.Max(0.1, x.EndTime - x.StartTime));

                var restaurantSegments = group.Count(x => x.Role == PhoneOrderRole.Restaurant);
                var firstSegmentBonus = string.Equals(group.Key, firstSpeaker, StringComparison.OrdinalIgnoreCase) ? 1.5 : 0;
                var score = (restaurantDuration * 3) + restaurantSegments + firstSegmentBonus - clientDuration;

                return new
                {
                    Speaker = group.Key,
                    Score = score,
                    RestaurantDuration = restaurantDuration,
                    ClientDuration = clientDuration,
                    FirstStartTime = group.Min(x => x.StartTime)
                };
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.FirstStartTime)
            .ToList();

        var restaurantSpeaker = speakerScores.FirstOrDefault()?.Speaker ?? firstSpeaker;

        Log.Information("Diarized speaker scorecard: {@SpeakerScores}", speakerScores);

        return restaurantSpeaker;
    }

    private List<PhoneOrderDiarizedSpeakInfoDto> StitchFragmentedDiarizedSegments(List<PhoneOrderDiarizedSpeakInfoDto> speakInfos)
    {
        if (speakInfos is not { Count: > 1 })
            return speakInfos;

        var stitchedSpeakInfos = new List<PhoneOrderDiarizedSpeakInfoDto>();

        foreach (var speakInfo in speakInfos)
        {
            if (stitchedSpeakInfos.Count == 0)
            {
                stitchedSpeakInfos.Add(speakInfo);
                continue;
            }

            var lastSpeakInfo = stitchedSpeakInfos[^1];
            var gap = Math.Max(0, speakInfo.StartTime - lastSpeakInfo.EndTime);

            if (ShouldStitchFragmentedSegment(lastSpeakInfo, speakInfo, gap))
            {
                Log.Information(
                    "Stitch fragmented diarized segments. Previous: {PreviousText}, Current: {CurrentText}, Gap: {Gap}", lastSpeakInfo.Text, speakInfo.Text, gap);

                lastSpeakInfo.EndTime = Math.Max(lastSpeakInfo.EndTime, speakInfo.EndTime);
                lastSpeakInfo.Text = JoinSegmentText(lastSpeakInfo.Text, speakInfo.Text);
                continue;
            }

            stitchedSpeakInfos.Add(speakInfo);
        }

        return stitchedSpeakInfos;
    }

    private static bool ShouldStitchFragmentedSegment(PhoneOrderDiarizedSpeakInfoDto previous, PhoneOrderDiarizedSpeakInfoDto current, double gap)
    {
        if (!string.Equals(previous.Speaker, current.Speaker, StringComparison.OrdinalIgnoreCase))
            return false;

        if (previous.Role != current.Role)
            return false;

        if (gap > 1.6)
            return false;

        return LooksLikeTrailingFragment(previous.Text) ||
               LooksLikeLeadingFragment(current.Text) ||
               LooksLikeSplitQuantity(previous.Text, current.Text) ||
               LooksLikeSentenceContinuation(previous.Text, current.Text);
    }

    private static bool LooksLikeTrailingFragment(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();

        if (trimmed.EndsWith(",") || trimmed.EndsWith(";") || trimmed.EndsWith(":"))
            return true;

        var lastToken = ExtractLastToken(trimmed);

        if (string.IsNullOrWhiteSpace(lastToken))
            return false;

        return IsNumericOrQuantityWord(lastToken) ||
               QuantityUnitWords.Contains(lastToken) ||
               FragmentLeadWords.Contains(lastToken);
    }

    private static bool LooksLikeLeadingFragment(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var firstToken = ExtractFirstToken(text);

        if (string.IsNullOrWhiteSpace(firstToken))
            return false;

        return QuantityUnitWords.Contains(firstToken) ||
               FragmentLeadWords.Contains(firstToken) ||
               firstToken.Equals("of", StringComparison.OrdinalIgnoreCase) ||
               IsLowerCaseWord(firstToken);
    }

    private static bool LooksLikeSplitQuantity(string previousText, string currentText)
    {
        var previousLastToken = ExtractLastToken(previousText);
        var currentFirstToken = ExtractFirstToken(currentText);

        if (string.IsNullOrWhiteSpace(previousLastToken) || string.IsNullOrWhiteSpace(currentFirstToken))
            return false;

        return (IsNumericOrQuantityWord(previousLastToken) && QuantityUnitWords.Contains(currentFirstToken)) ||
               (QuantityLeadWords.Contains(previousLastToken) && currentFirstToken.Equals("of", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeSentenceContinuation(string previousText, string currentText)
    {
        if (string.IsNullOrWhiteSpace(previousText) || string.IsNullOrWhiteSpace(currentText))
            return false;

        var trimmedPrevious = previousText.Trim();
        var trimmedCurrent = currentText.Trim();

        return !EndsWithSentencePunctuation(trimmedPrevious) &&
               !char.IsUpper(trimmedCurrent[0]);
    }

    private static string JoinSegmentText(string previousText, string currentText)
    {
        if (string.IsNullOrWhiteSpace(previousText))
            return currentText?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(currentText))
            return previousText.Trim();

        var left = previousText.Trim();
        var right = currentText.Trim();

        return right.StartsWith(",", StringComparison.Ordinal) || right.StartsWith(".", StringComparison.Ordinal)
            ? left + right
            : left + " " + right;
    }

    private static string ExtractFirstToken(string text)
    {
        return Regex.Match(text ?? string.Empty, @"[\p{L}\p{N}]+").Value;
    }

    private static string ExtractLastToken(string text)
    {
        var matches = Regex.Matches(text ?? string.Empty, @"[\p{L}\p{N}]+");
        return matches.Count == 0 ? string.Empty : matches[^1].Value;
    }

    private static bool IsNumericOrQuantityWord(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        return Regex.IsMatch(token, @"^\d+(?:\.\d+)?$") || QuantityLeadWords.Contains(token);
    }

    private static bool EndsWithSentencePunctuation(string text)
    {
        return text.EndsWith(".") || text.EndsWith("!") || text.EndsWith("?");
    }

    private static bool IsLowerCaseWord(string token)
    {
        return token.Any(char.IsLetter) && token.All(ch => !char.IsLetter(ch) || char.IsLower(ch));
    }

    private static bool TryParseDiarizedRole(string roleText, out PhoneOrderRole role)
    {
        role = default;

        if (string.IsNullOrWhiteSpace(roleText))
            return false;

        var normalizedRole = roleText.Trim().ToLowerInvariant();

        switch (normalizedRole)
        {
            case "restaurant":
            case "agent":
            case "assistant":
            case "客服":
            case "餐厅":
            case "商家":
                role = PhoneOrderRole.Restaurant;
                return true;
            case "client":
            case "customer":
            case "user":
            case "caller":
            case "用户":
            case "顾客":
            case "客户":
                role = PhoneOrderRole.Client;
                return true;
            default:
                return false;
        }
    }

    private static string NormalizeSegmentText(string text)
    {
        return Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();
    }
}
