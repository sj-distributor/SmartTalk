using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Requests.AutoTest;

namespace SmartTalk.Core.Handlers.RequestHandlers.AutoTest;

public class GetAutoTestTestTaskRequestHandler : IRequestHandler<GetAutoTestTestTaskRequest, GetAutoTestTestTaskResponse>
{
    private readonly IAutoTestService _autoTestService;

    public GetAutoTestTestTaskRequestHandler(IAutoTestService autoTestService)
    {
        _autoTestService = autoTestService;
    }
    
    public async Task<GetAutoTestTestTaskResponse> Handle(IReceiveContext<GetAutoTestTestTaskRequest> context, CancellationToken cancellationToken)
    {
        return await _autoTestService.GetAutoTestTestTasksAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}