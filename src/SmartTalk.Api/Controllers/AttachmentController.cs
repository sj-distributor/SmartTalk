using Mediator.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Dto.Attachments;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AttachmentController : ControllerBase
{
    private readonly IMediator _mediator;

    public AttachmentController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [RequestSizeLimit(157_286_400)]
    [Route("upload"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UploadAttachmentResponse))]
    public async Task<IActionResult> UploadAttachmentAsync([FromForm] IFormFile file, [FromQuery] string? fileIndex) 
    {
        var ms = new MemoryStream();

        await file.CopyToAsync(ms).ConfigureAwait(false);

        var fileContent = ms.ToArray();

        var uploadAttachmentDto = new UploadAttachmentDto
        {
            FileName = file.FileName, FileContent = fileContent
        };

        if (fileIndex != null) uploadAttachmentDto.FileIndex = fileIndex;

        var response = await _mediator.SendAsync<UploadAttachmentCommand, UploadAttachmentResponse>(
            new UploadAttachmentCommand
            {
                Attachment = uploadAttachmentDto
            }).ConfigureAwait(false);
        
        return Ok(response);
    }
}