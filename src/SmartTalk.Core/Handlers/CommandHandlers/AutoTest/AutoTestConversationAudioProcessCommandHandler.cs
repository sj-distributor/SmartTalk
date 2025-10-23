using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;

namespace SmartTalk.Core.Handlers.CommandHandlers.AutoTest;

public class AutoTestConversationAudioProcessCommandHandler : ICommandHandler<AutoTestConversationAudioProcessCommand, AutoTestConversationAudioProcessReponse>
{
    private readonly IAutoTestService _autoTestService;

    public AutoTestConversationAudioProcessCommandHandler(IAutoTestService autoTestService)
    {
        _autoTestService = autoTestService;
    }
    
    public async Task<AutoTestConversationAudioProcessReponse> Handle(IReceiveContext<AutoTestConversationAudioProcessCommand> context, CancellationToken cancellationToken)
    {
        return await _autoTestService.AutoTestConversationAudioProcessAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}