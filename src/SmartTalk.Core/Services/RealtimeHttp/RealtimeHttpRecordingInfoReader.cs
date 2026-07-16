using System.Text.Json;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.RealtimeHttp;
using SmartTalk.Messages.Dto.RealtimeHttp;

namespace SmartTalk.Core.Services.RealtimeHttp;

public interface IRealtimeHttpRecordingInfoReader : ISingletonDependency
{
    Task<RealtimeHttpRecordingInfoResponse> GetRecordingInfoAsync(
        string sessionId,
        string providerSessionId,
        Func<string, string> buildDownloadUrl,
        CancellationToken cancellationToken);
}

public sealed class RealtimeHttpRecordingInfoReader : IRealtimeHttpRecordingInfoReader
{
    private readonly RealtimeHttpGatewaySettings _settings;

    public RealtimeHttpRecordingInfoReader(RealtimeHttpGatewaySettings settings)
    {
        _settings = settings;
    }

    public async Task<RealtimeHttpRecordingInfoResponse> GetRecordingInfoAsync(
        string sessionId,
        string providerSessionId,
        Func<string, string> buildDownloadUrl,
        CancellationToken cancellationToken)
    {
        var response = new RealtimeHttpRecordingInfoResponse
        {
            SessionId = sessionId,
            ProviderSessionId = providerSessionId
        };

        if (string.IsNullOrWhiteSpace(providerSessionId))
        {
            response.Ready = false;
            response.Message = "provider_session_not_resolved";
            return response;
        }

        var metadata = await TryLoadRecordingMetadataAsync(providerSessionId, cancellationToken).ConfigureAwait(false);
        if (metadata == null)
        {
            response.Ready = false;
            response.Message = "recording_metadata_not_ready";
            return response;
        }

        response.ProcessedAt = metadata.ProcessedAt;
        response.Transcriptions = await TryLoadTranscriptionsAsync(providerSessionId, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(metadata.RecordingPath))
        {
            response.Ready = false;
            response.Message = "recording_path_empty";
            return response;
        }

        response.RecordingPath = metadata.RecordingPath;
        response.RecordingFileName = Path.GetFileName(metadata.RecordingPath);
        response.RecordingFileSize = metadata.RecordingFileSize;

        if (!File.Exists(metadata.RecordingPath))
        {
            response.Ready = false;
            response.Message = "recording_file_not_found";
            return response;
        }

        if (response.RecordingFileSize <= 0)
            response.RecordingFileSize = new FileInfo(metadata.RecordingPath).Length;

        response.Ready = true;
        response.Message = "ok";
        response.DownloadUrl = buildDownloadUrl(providerSessionId);
        return response;
    }

    private async Task<RecordingMetadata> TryLoadRecordingMetadataAsync(string providerSessionId, CancellationToken cancellationToken)
    {
        var storageRoot = GetRecordingStorageRoot();
        if (string.IsNullOrWhiteSpace(storageRoot))
            return null;

        var metadataPath = Path.Combine(storageRoot, _settings.RecordingProcessedFolder, $"{providerSessionId}.json");
        if (!File.Exists(metadataPath))
            return null;

        var json = await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var rawRecordingPath = TryReadString(root, "RecordingUrl");
        var normalizedRecordingPath = NormalizeRecordingPath(rawRecordingPath, storageRoot);
        var recordingFileSize = TryReadLong(root, "RecordingFileSize");
        var processedAt = TryReadDateTimeOffset(root, "ProcessedAt");

        if (recordingFileSize <= 0 && !string.IsNullOrWhiteSpace(normalizedRecordingPath) && File.Exists(normalizedRecordingPath))
            recordingFileSize = new FileInfo(normalizedRecordingPath).Length;

        return new RecordingMetadata
        {
            RecordingPath = normalizedRecordingPath,
            RecordingFileSize = recordingFileSize,
            ProcessedAt = processedAt
        };
    }

    private async Task<List<RealtimeHttpTranscriptionItemDto>> TryLoadTranscriptionsAsync(string providerSessionId, CancellationToken cancellationToken)
    {
        var storageRoot = GetRecordingStorageRoot();
        if (string.IsNullOrWhiteSpace(storageRoot))
            return [];

        var callbackPath = Path.Combine(storageRoot, _settings.RecordingCallbackFolder, $"aikid-conversation-{providerSessionId}.json");
        if (!File.Exists(callbackPath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(callbackPath, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Transcriptions", out var transcriptions)
                || transcriptions.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var result = new List<RealtimeHttpTranscriptionItemDto>();
            foreach (var item in transcriptions.EnumerateArray())
            {
                var transcription = TryReadString(item, "Transcription");
                if (string.IsNullOrWhiteSpace(transcription))
                    continue;

                result.Add(new RealtimeHttpTranscriptionItemDto
                {
                    Speaker = TryReadInt(item, "Speaker"),
                    Transcription = transcription
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[RealtimeHttpGateway] Failed to parse transcription callback, ProviderSessionId: {ProviderSessionId}", providerSessionId);
            return [];
        }
    }

    private string GetRecordingStorageRoot()
    {
        if (string.IsNullOrWhiteSpace(_settings.RecordingStorageBasePath))
            return string.Empty;

        try
        {
            return Path.GetFullPath(_settings.RecordingStorageBasePath);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string NormalizeRecordingPath(string rawPath, string storageRoot)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return string.Empty;

        var fullPath = Path.GetFullPath(Path.IsPathRooted(rawPath) ? rawPath : Path.Combine(storageRoot, rawPath));
        var normalizedStorageRoot = Path.GetFullPath(storageRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                    + Path.DirectorySeparatorChar;
        var normalizedFullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!normalizedFullPath.StartsWith(normalizedStorageRoot, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return fullPath;
    }

    private static string TryReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
            return string.Empty;

        return value.GetString() ?? string.Empty;
    }

    private static long TryReadLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return 0;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
            _ => 0
        };
    }

    private static int TryReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return 0;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => 0
        };
    }

    private static DateTimeOffset? TryReadDateTimeOffset(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private sealed class RecordingMetadata
    {
        public string RecordingPath { get; init; } = string.Empty;

        public long RecordingFileSize { get; init; }

        public DateTimeOffset? ProcessedAt { get; init; }
    }
}
