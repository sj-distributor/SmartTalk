using System.Net;
using System.Runtime.ExceptionServices;
using Mediator.Net.Context;
using Mediator.Net.Contracts;
using Mediator.Net.Pipeline;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Messages.Requests.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Core.Middlewares.Security;

public class SecurityMiddleWareSpecification<TContext> : IPipeSpecification<TContext>
    where TContext : IContext<IMessage>
{
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityService _identityService;

    public SecurityMiddleWareSpecification(ICurrentUser currentUser, IIdentityService identityService)
    {
        _currentUser = currentUser;
        _identityService = identityService;
    }
    
    public bool ShouldExecute(TContext context, CancellationToken cancellationToken)
    {
        return true;
    }

    public async Task BeforeExecute(TContext context, CancellationToken cancellationToken)
    {
        if (_currentUser.Id.HasValue)
        {
            var isCurrentUserExist = await _identityService.IsCurrentUserExistAsync(_currentUser.Id.Value, cancellationToken).ConfigureAwait(false);

            if (!isCurrentUserExist)
                throw new AccountExpiredException("User Account Is Not Exist");
        }
    }

    public Task Execute(TContext context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task AfterExecute(TContext context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task OnException(Exception ex, TContext context)
    {
        ExceptionDispatchInfo.Capture(ex).Throw();
        throw ex;
    }
}