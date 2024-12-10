using System.Runtime.ExceptionServices;
using Mediator.Net.Context;
using Mediator.Net.Contracts;
using Mediator.Net.Pipeline;
using Serilog;
using SmartTalk.Core.Services.Identity;

namespace SmartTalk.Core.Middlewares.Authorization;

public class AuthorizationMiddlewareSpecification<TContext> : IPipeSpecification<TContext>
    where TContext : IContext<IMessage>
{
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityService _identityService;

    public AuthorizationMiddlewareSpecification(ICurrentUser currentUser, IIdentityService identityService)
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
        Log.Information("AuthrizationMiddelwareSpecification start time: {datetime}", DateTime.Now.ToString("HH:mm:ss zz"));
        
        var (requiredRoles, requiredPermissions) =
            _identityService.GetRolesAndPermissionsFromAttributes(context.Message.GetType());

        if (requiredRoles.Any() || requiredPermissions.Any())
        {
            var requiredRolesOrPermissions = requiredRoles.Concat(requiredPermissions).ToList();

            var isInRole = await _identityService
                .IsInRolesAsync(_currentUser.Id, requiredRolesOrPermissions, cancellationToken).ConfigureAwait(false);

            if (!isInRole)
                throw new ForbiddenAccessException();
        }
        
        Log.Information("AuthrizationMiddelwareSpecification end time: {datetime}", DateTime.Now.ToString("HH:mm:ss zz"));
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