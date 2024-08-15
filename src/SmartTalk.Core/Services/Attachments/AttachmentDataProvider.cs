using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Attachments;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.Attachments;

namespace SmartTalk.Core.Services.Attachments;

public interface IAttachmentDataProvider : IScopedDependency
{
    Task AddAttachmentAsync(
        Attachment attachment, CancellationToken cancellationToken);
    
    Task AddAttachmentsAsync(
        List<Attachment> attachments, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<List<AttachmentDto>> GetAttachmentsByIdsAsync(
        List<int> attachmentIds, CancellationToken cancellationToken);

    Task<AttachmentDto> GetAttachmentByIdAsync(int attachmentId, CancellationToken cancellationToken);
}

public class AttachmentDataProvider : IAttachmentDataProvider
{
    private readonly IMapper _mapper;
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    
    public AttachmentDataProvider(IMapper mapper, IRepository repository, IUnitOfWork unitOfWork)
    {
        _mapper = mapper;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }
    
    public async Task AddAttachmentAsync(
        Attachment attachment, CancellationToken cancellationToken)
    {
        await _repository.InsertAsync(attachment, cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAttachmentsAsync(
        List<Attachment> attachments, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(attachments, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AttachmentDto>> GetAttachmentsByIdsAsync(
        List<int> attachmentIds, CancellationToken cancellationToken)
    {
        if (attachmentIds is { Count: > 0 })
        {
            return await _repository.Query<Attachment>()
                .Where(x => attachmentIds.Contains(x.Id))
                .ProjectTo<AttachmentDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return null;
    }

    public async Task<AttachmentDto> GetAttachmentByIdAsync(int attachmentId, CancellationToken cancellationToken)
    {
        return await _repository.Query<Attachment>()
            .Where(x => x.Id == attachmentId)
            .ProjectTo<AttachmentDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}