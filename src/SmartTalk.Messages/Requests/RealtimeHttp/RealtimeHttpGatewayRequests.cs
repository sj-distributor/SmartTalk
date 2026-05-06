using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.RealtimeHttp;

namespace SmartTalk.Messages.Requests.RealtimeHttp;

public class GetRealtimeHttpSessionsRequest : IRequest
{
}

public class GetRealtimeHttpSessionsResponse : IResponse
{
    public IReadOnlyList<RealtimeHttpSessionDetailResponse> Sessions { get; set; } = [];
}

public class GetRealtimeHttpSessionRequest : IRequest
{
    public string SessionId { get; set; } = string.Empty;
}

public class GetRealtimeHttpSessionResponse : IResponse
{
    public bool Found { get; set; }

    public RealtimeHttpSessionDetailResponse Session { get; set; } = new();
}

public class GetRealtimeHttpRecordingInfoRequest : IRequest
{
    public string SessionIdOrProviderSessionId { get; set; } = string.Empty;
}
