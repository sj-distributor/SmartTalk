using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.RealtimeHttp;
using SmartTalk.Messages.Dto.RealtimeHttp;
using SmartTalk.Messages.Requests.RealtimeHttp;

namespace SmartTalk.Core.Handlers.RequestHandlers.RealtimeHttp;

public class GetRealtimeHttpSessionsRequestHandler : IRequestHandler<GetRealtimeHttpSessionsRequest, GetRealtimeHttpSessionsResponse>
{
    private readonly IRealtimeHttpGatewayService _service;

    public GetRealtimeHttpSessionsRequestHandler(IRealtimeHttpGatewayService service)
    {
        _service = service;
    }

    public Task<GetRealtimeHttpSessionsResponse> Handle(IReceiveContext<GetRealtimeHttpSessionsRequest> context, CancellationToken cancellationToken)
    {
        return Task.FromResult(new GetRealtimeHttpSessionsResponse
        {
            Sessions = _service.ListSessions()
        });
    }
}

public class GetRealtimeHttpSessionRequestHandler : IRequestHandler<GetRealtimeHttpSessionRequest, GetRealtimeHttpSessionResponse>
{
    private readonly IRealtimeHttpGatewayService _service;

    public GetRealtimeHttpSessionRequestHandler(IRealtimeHttpGatewayService service)
    {
        _service = service;
    }

    public async Task<GetRealtimeHttpSessionResponse> Handle(IReceiveContext<GetRealtimeHttpSessionRequest> context, CancellationToken cancellationToken)
    {
        var session = await _service.GetSessionAsync(context.Message.SessionId).ConfigureAwait(false);
        return new GetRealtimeHttpSessionResponse
        {
            Found = session != null,
            Session = session ?? new RealtimeHttpSessionDetailResponse()
        };
    }
}

public class GetRealtimeHttpRecordingInfoRequestHandler : IRequestHandler<GetRealtimeHttpRecordingInfoRequest, RealtimeHttpRecordingInfoResponse>
{
    private readonly IRealtimeHttpGatewayService _service;

    public GetRealtimeHttpRecordingInfoRequestHandler(IRealtimeHttpGatewayService service)
    {
        _service = service;
    }

    public async Task<RealtimeHttpRecordingInfoResponse> Handle(IReceiveContext<GetRealtimeHttpRecordingInfoRequest> context, CancellationToken cancellationToken)
    {
        return await _service.GetRecordingInfoAsync(context.Message.SessionIdOrProviderSessionId, cancellationToken).ConfigureAwait(false);
    }
}
