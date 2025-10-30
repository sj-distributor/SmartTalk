using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Messages.Commands.SpeechMatics;

namespace SmartTalk.Core.Handlers.CommandHandlers.SpeechMatics;

public class CreateSpeechMaticsJobCommandHandler : ICommandHandler<CreateSpeechmaticsJobCommand, CreateSpeechmaticsJobResponse>
{
    private readonly ISpeechMaticsService _speechMaticsService;

    public CreateSpeechMaticsJobCommandHandler(ISpeechMaticsService speechMaticsService)
    {
        _speechMaticsService = speechMaticsService;
    }

    public async Task<CreateSpeechmaticsJobResponse> Handle(IReceiveContext<CreateSpeechmaticsJobCommand> context, CancellationToken cancellationToken)
    {
        return await _speechMaticsService.CreateSpeechMaticsJobAsync(context.Message, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}