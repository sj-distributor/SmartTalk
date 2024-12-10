using Mediator.Net.Context;
using Mediator.Net.Pipeline;
using Mediator.Net.Contracts;
using SmartTalk.Core.Extensions;
using System.Runtime.ExceptionServices;

namespace SmartTalk.Core.Middlewares.FluentMessageValidator
{
    public class MessageValidatorMiddlewareSpecification<TContext> : IPipeSpecification<TContext>
        where TContext : IContext<IMessage>
    {
        private readonly IEnumerable<IFluentMessageValidator> _messageValidators;

        public MessageValidatorMiddlewareSpecification(IEnumerable<IFluentMessageValidator> messageValidators)
        {
            _messageValidators = messageValidators;
        }

        public bool ShouldExecute(TContext context, CancellationToken cancellationToken)
        {
            return true;
        }

        public Task BeforeExecute(TContext context, CancellationToken cancellationToken)
        {
            return Task.WhenAll();
        }

        public Task Execute(TContext context, CancellationToken cancellationToken)
        {
            if (ShouldExecute(context, cancellationToken))
            {
                _messageValidators
                    .Where(x => x.ForMessageType == context.Message.GetType())
                    .ForEach(v => v.ValidateMessage(context.Message));
            }
            return Task.WhenAll();
        }

        public Task AfterExecute(TContext context, CancellationToken cancellationToken)
        {
            return Task.WhenAll();
        }

        public Task OnException(Exception ex, TContext context)
        {
            ExceptionDispatchInfo.Capture(ex).Throw();
            throw ex;
        }
    }
}
