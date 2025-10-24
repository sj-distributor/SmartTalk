using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Requests.AutoTest;

namespace SmartTalk.Core.Handlers.RequestHandlers.AutoTest;

public class GetAutoTestTestTaskRequestHandler : IRequestHandler<GetAutoTestTaskRequest, GetAutoTestTaskResponse>
{
    private readonly IAutoTestService _autoTestService;

    public GetAutoTestTestTaskRequestHandler(IAutoTestService autoTestService)
    {
        _autoTestService = autoTestService;
    }
    
    public async Task<GetAutoTestTaskResponse> Handle(IReceiveContext<GetAutoTestTaskRequest> context, CancellationToken cancellationToken)
    {
        return await _autoTestService.GetAutoTestTasksAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}