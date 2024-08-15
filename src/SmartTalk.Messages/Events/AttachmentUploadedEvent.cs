using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Attachments;

namespace SmartTalk.Messages.Events;

public class AttachmentUploadedEvent : IEvent
{
    public AttachmentDto Attachment { get; set; }
}