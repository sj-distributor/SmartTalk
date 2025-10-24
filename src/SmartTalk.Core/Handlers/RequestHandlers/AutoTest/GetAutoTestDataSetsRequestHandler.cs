using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Requests.AutoTest;

namespace SmartTalk.Core.Handlers.RequestHandlers.AutoTest;

public class GetAutoTestDataSetsRequestHandler : IRequestHandler<GetAutoTestDataSetRequest, GetAutoTestDataSetResponse>
{
    private readonly IAutoTestService _autoTestService;

    public GetAutoTestDataSetsRequestHandler(IAutoTestService autoTestService)
    {
        _autoTestService = autoTestService;
    }

    public async Task<GetAutoTestDataSetResponse> Handle(IReceiveContext<GetAutoTestDataSetRequest> context, CancellationToken cancellationToken)
    {
        return await _autoTestService.GetAutoTestDataSetsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}