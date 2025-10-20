using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Security;
using SmartTalk.Messages.Commands.Security;

namespace SmartTalk.Core.Handlers.CommandHandlers.Security;

public class SwitchLanguageCommandHandler : ICommandHandler<SwitchLanguageCommand, SwitchLanguageResponse>
{
    private readonly ISecurityService _securityService;
    
    public SwitchLanguageCommandHandler(ISecurityService securityService)
    {
        _securityService = securityService;
    }
    
    public async Task<SwitchLanguageResponse> Handle(IReceiveContext<SwitchLanguageCommand> context, CancellationToken cancellationToken)
    {
        return await _securityService.SwitchLanguageAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}