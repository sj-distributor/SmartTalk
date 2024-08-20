using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Messages.Commands.Attachments;

namespace SmartTalk.Core.Handlers.CommandHandlers.Attachments;

public class UploadAttachmentCommandHandler : ICommandHandler<UploadAttachmentCommand, UploadAttachmentResponse>
{
    private readonly IAttachmentService _attachmentService;

    public UploadAttachmentCommandHandler(IAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    public async Task<UploadAttachmentResponse> Handle(IReceiveContext<UploadAttachmentCommand> context, CancellationToken cancellationToken)
    {
        var attachmentUploadedEvent = await _attachmentService.UploadAttachmentAsync(context.Message, cancellationToken);

        await context.PublishAsync(attachmentUploadedEvent, cancellationToken).ConfigureAwait(false);

        return new UploadAttachmentResponse { Data = attachmentUploadedEvent.Attachment };
    }
}