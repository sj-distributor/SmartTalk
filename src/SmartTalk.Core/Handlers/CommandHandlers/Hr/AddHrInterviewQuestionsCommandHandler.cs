using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Hr;
using SmartTalk.Messages.Commands.Hr;

namespace SmartTalk.Core.Handlers.CommandHandlers.Hr;

public class AddHrInterviewQuestionsCommandHandler : ICommandHandler<AddHrInterviewQuestionsCommand>
{
    private readonly IHrService _hrService;

    public AddHrInterviewQuestionsCommandHandler(IHrService hrService)
    {
        _hrService = hrService;
    }

    public async Task Handle(IReceiveContext<AddHrInterviewQuestionsCommand> context, CancellationToken cancellationToken)
    {
        await _hrService.AddHrInterviewQuestionsAsync(context.Message, cancellationToken);
    }
}