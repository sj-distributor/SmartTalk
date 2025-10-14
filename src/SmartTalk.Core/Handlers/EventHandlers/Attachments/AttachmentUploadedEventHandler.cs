using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Events;

namespace SmartTalk.Core.Handlers.EventHandlers.Attachments;

public class AttachmentUploadedEventHandler : IEventHandler<AttachmentUploadedEvent>
{
    public Task Handle(IReceiveContext<AttachmentUploadedEvent> context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}