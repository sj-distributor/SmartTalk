using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Messages.Dto.RealtimeHttp;

public class RealtimeHttpCreateSessionRequest
{
    public int AssistantId { get; set; }

    public RealtimeAiServerRegion Region { get; set; } = RealtimeAiServerRegion.US;
}

public class RealtimeHttpCreateSessionResponse : IResponse
{
    public string SessionId { get; set; } = string.Empty;

    public string ProviderSessionId { get; set; } = string.Empty;

    public int AssistantId { get; set; }

    public RealtimeAiServerRegion Region { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

public class RealtimeHttpSendMessageRequest
{
    public string Text { get; set; } = string.Empty;

    public int? TimeoutMs { get; set; }
}

public class RealtimeHttpSendMessageResponse : IResponse
{
    public string SessionId { get; set; } = string.Empty;

    public string InputText { get; set; } = string.Empty;

    public string OutputText { get; set; } = string.Empty;

    public bool Completed { get; set; }

    public int TurnNumber { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public class RealtimeHttpRunDefaultConversationRequest
    : ICommand
{
    public int AssistantId { get; set; }

    public RealtimeAiServerRegion Region { get; set; } = RealtimeAiServerRegion.US;
}

public class RealtimeHttpConversationTurnResponse
{
    public int Index { get; set; }

    public string InputText { get; set; } = string.Empty;

    public string OutputText { get; set; } = string.Empty;

    public bool Completed { get; set; }

    public int TurnNumber { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public class RealtimeHttpRunDefaultConversationResponse : IResponse
{
    public string SessionId { get; set; } = string.Empty;

    public string ProviderSessionId { get; set; } = string.Empty;

    public int AssistantId { get; set; }

    public RealtimeAiServerRegion Region { get; set; }

    public bool WarmupTurnCompleted { get; set; }

    public int WarmupTurnNumber { get; set; }

    public List<RealtimeHttpConversationTurnResponse> Turns { get; set; } = [];

    public bool Closed { get; set; }

    public string CloseReason { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset EndedAt { get; set; }
}

public class RealtimeHttpSessionEventDto
{
    public long Sequence { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

public class RealtimeHttpSessionDetailResponse : IResponse
{
    public string SessionId { get; set; } = string.Empty;

    public string ProviderSessionId { get; set; } = string.Empty;

    public int AssistantId { get; set; }

    public RealtimeAiServerRegion Region { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastActivityAt { get; set; }

    public string LastError { get; set; } = string.Empty;

    public int CompletedTurns { get; set; }

    public List<RealtimeHttpSessionEventDto> RecentEvents { get; set; } = [];
}

public class RealtimeHttpDisconnectResponse : IResponse
{
    public string SessionId { get; set; } = string.Empty;

    public string ProviderSessionId { get; set; } = string.Empty;

    public bool Closed { get; set; }

    public string Reason { get; set; } = string.Empty;
}

public class RealtimeHttpRecordingInfoResponse : IResponse
{
    public string SessionId { get; set; } = string.Empty;

    public string ProviderSessionId { get; set; } = string.Empty;

    public bool Ready { get; set; }

    public string Message { get; set; } = string.Empty;

    public string RecordingFileName { get; set; } = string.Empty;

    public string RecordingPath { get; set; } = string.Empty;

    public long RecordingFileSize { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public string DownloadUrl { get; set; } = string.Empty;

    public List<RealtimeHttpTranscriptionItemDto> Transcriptions { get; set; } = [];
}

public class RealtimeHttpTranscriptionItemDto
{
    public int Speaker { get; set; }

    public string Transcription { get; set; } = string.Empty;
}
