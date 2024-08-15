using AutoMapper;
using Serilog;
using SmartTalk.Core.Domain.Attachments;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AliYun;
using SmartTalk.Core.Utils;
using SmartTalk.Messages.Dto.Attachments;

namespace SmartTalk.Core.Services.Attachments;

public interface IAttachmentUtilService : IScopedDependency
{
    Task<List<AttachmentDto>> UploadFilesAsync(List<UploadAttachmentDto> files, CancellationToken cancellationToken = default);
}

public class AttachmentUtilService : IAttachmentUtilService
{
    private readonly IMapper _mapper;
    private readonly IAliYunOssService _ossService;
    private readonly IAttachmentDataProvider _attachmentDataProvider;

    public AttachmentUtilService(IMapper mapper, IAliYunOssService ossService, IAttachmentDataProvider attachmentDataProvider)
    {
        _mapper = mapper;
        _ossService = ossService;
        _attachmentDataProvider = attachmentDataProvider;
    }

    public async Task<List<AttachmentDto>> UploadFilesAsync(List<UploadAttachmentDto> files, CancellationToken cancellationToken = default)
    {
        if (files == null || !files.Any()) throw new ArgumentNullException(nameof(files));

        var attachments = new List<Attachment>();
        
        foreach (var file in files)
        {
            var originFileName = file.FileName;
            
            GenerateNewFileNameForAttachment(file);
            
            ValidAttachment(file);
        
            try
            {
                var fullFile = $"{file.FileIndex}/{file.FileName}";
                
                _ossService.UploadFile(fullFile, file.FileContent);

                var attachment = new Attachment
                {
                    FilePath = fullFile,
                    Uuid = file.Uuid,
                    FileName = file.FileName,
                    OriginFileName = originFileName,
                    FileSize = file.FileContent.Length,
                    FileUrl = HttpUrlUtil.ReplaceHttpWithHttps(_ossService.GetFileUrl(fullFile))
                };

                attachments.Add(attachment);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Attachment upload failed, {FileName}", file.FileName); 
            }
        }
        
        await _attachmentDataProvider.AddAttachmentsAsync(attachments, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return _mapper.Map<List<AttachmentDto>>(attachments);
    }
    
    private void GenerateNewFileNameForAttachment(UploadAttachmentDto attachment)
    {
        attachment.FileName = $"{attachment.Uuid}{Path.GetExtension(attachment.FileName)}";
    }
        
    private void ValidAttachment(UploadAttachmentDto attachment)
    {
        if (attachment?.FileContent == null)
            throw new ArgumentNullException(nameof(attachment.FileContent));
    }
}