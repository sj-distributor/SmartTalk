using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AliYun;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Events;

namespace SmartTalk.Core.Services.Attachments;

public interface IAttachmentService : IScopedDependency
{
    Task<AttachmentUploadedEvent> UploadAttachmentAsync(UploadAttachmentCommand command, CancellationToken cancellationToken);
}

 public class AttachmentService : IAttachmentService
    {
        private readonly IMapper _mapper;
        private readonly IAliYunOssService _ossService;
        private readonly IAttachmentUtilService _attachmentUtilService;
        private readonly IAttachmentDataProvider _attachmentDataProvider;

        public AttachmentService(
            IMapper mapper,
            IAliYunOssService ossService,
            IAttachmentUtilService attachmentUtilService,
            IAttachmentDataProvider attachmentDataProvider)
        {
            _mapper = mapper;
            _ossService = ossService;
            _attachmentUtilService = attachmentUtilService;
            _attachmentDataProvider = attachmentDataProvider;
        }

        public async Task<AttachmentUploadedEvent> UploadAttachmentAsync(
            UploadAttachmentCommand command, CancellationToken cancellationToken = default)
        {
            var attachment = (await _attachmentUtilService
                .UploadFilesAsync(new List<UploadAttachmentDto>{ command.Attachment }, cancellationToken).ConfigureAwait(false)).FirstOrDefault();

            return attachment == null ? new AttachmentUploadedEvent() : new AttachmentUploadedEvent { Attachment = attachment };
        }
    }