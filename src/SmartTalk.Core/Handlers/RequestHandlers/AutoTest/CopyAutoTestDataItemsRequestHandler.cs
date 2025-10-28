using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Requests.AutoTest;

namespace SmartTalk.Core.Handlers.RequestHandlers.AutoTest;

public class CopyAutoTestDataItemsRequestHandler : IRequestHandler<CopyAutoTestDataSetRequest, CopyAutoTestDataSetResponse>
{
    private readonly IAutoTestService _autoTestService;

    public CopyAutoTestDataItemsRequestHandler(IAutoTestService autoTestService)
    {
        _autoTestService = autoTestService;
    }

    public async Task<CopyAutoTestDataSetResponse> Handle(IReceiveContext<CopyAutoTestDataSetRequest> context, CancellationToken cancellationToken)
    {
        return await _autoTestService.CopyAutoTestDataItemsAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}