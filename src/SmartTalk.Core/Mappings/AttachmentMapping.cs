using AutoMapper;
using SmartTalk.Core.Domain.Attachments;
using SmartTalk.Messages.Dto.Attachments;

namespace SmartTalk.Core.Mappings;

public class AttachmentMapping : Profile
{
    public AttachmentMapping()
    {
        CreateMap<Attachment, AttachmentDto>();
    }
}