using System.Net;
using Smarties.Messages.DTO.Attachments;

namespace SmartTalk.Messages.Dto.Smarties;

public class UploadAttachmentResponse
{
    public AttachmentDto Data { get; set; }
    
    public string Msg { get; set; }

    public HttpStatusCode Code { get; set; }
}