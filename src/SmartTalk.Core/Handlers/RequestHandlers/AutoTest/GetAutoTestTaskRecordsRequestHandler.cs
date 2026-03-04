using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Requests.AutoTest;

namespace SmartTalk.Core.Handlers.RequestHandlers.AutoTest;

public class GetAutoTestTaskRecordsRequestHandler : IRequestHandler<GetAutoTestTaskRecordsRequest, GetAutoTestTaskRecordsResponse>
{
    private readonly IAutoTestService _autoTestService;

    public GetAutoTestTaskRecordsRequestHandler(IAutoTestService autoTestService)
    {
        _autoTestService = autoTestService;
    }

    public async Task<GetAutoTestTaskRecordsResponse> Handle(IReceiveContext<GetAutoTestTaskRecordsRequest> context, CancellationToken cancellationToken)
    {
        return await _autoTestService.GetAutoTestTaskRecordsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}