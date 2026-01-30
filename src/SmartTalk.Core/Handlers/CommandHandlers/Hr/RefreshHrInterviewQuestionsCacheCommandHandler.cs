using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Hr;
using SmartTalk.Messages.Commands.Hr;

namespace SmartTalk.Core.Handlers.CommandHandlers.Hr;

public class RefreshHrInterviewQuestionsCacheCommandHandler : ICommandHandler<RefreshHrInterviewQuestionsCacheCommand>
{
    private readonly IHrJobProcessJobService _hrJobProcessJobService;

    public RefreshHrInterviewQuestionsCacheCommandHandler(IHrJobProcessJobService hrJobProcessJobService)
    {
        _hrJobProcessJobService = hrJobProcessJobService;
    }

    public async Task Handle(IReceiveContext<RefreshHrInterviewQuestionsCacheCommand> context, CancellationToken cancellationToken)
    {
        await _hrJobProcessJobService.RefreshHrInterviewQuestionsCacheAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}