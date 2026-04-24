using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Requests.AutoTest;

namespace SmartTalk.Core.Handlers.RequestHandlers.AutoTest;

public class GetAutoTestDataItemsByIdRequestHandler : IRequestHandler<GetAutoTestDataItemsByIdRequest, GetAutoTestDataItemsByIdResponse>
{
    private readonly IAutoTestService _autoTestService;

    public GetAutoTestDataItemsByIdRequestHandler(IAutoTestService autoTestService)
    {
        _autoTestService = autoTestService;
    }

    public async Task<GetAutoTestDataItemsByIdResponse> Handle(IReceiveContext<GetAutoTestDataItemsByIdRequest> context, CancellationToken cancellationToken)
    {
        return await _autoTestService.GetAutoTestDataItemsByIdAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}