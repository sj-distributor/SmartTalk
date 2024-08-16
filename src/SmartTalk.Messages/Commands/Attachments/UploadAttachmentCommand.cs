using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Attachments;

public class UploadAttachmentCommand : ICommand
{
    public UploadAttachmentDto Attachment { get; set; }
}

public class UploadAttachmentResponse : SmartTalkResponse<AttachmentDto>
{
}