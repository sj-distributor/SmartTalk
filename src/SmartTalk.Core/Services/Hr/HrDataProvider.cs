using AutoMapper;
using AutoMapper.QueryableExtensions;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Hr;
using SmartTalk.Messages.Enums.Hr;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Dto.Hr;

namespace SmartTalk.Core.Services.Hr;

public interface IHrDataProvider : IScopedDependency
{
    Task<List<HrInterviewQuestion>> GetHrInterviewQuestionsAsync(
        HrInterviewQuestionSection? section = null, bool? isUsing = null, CancellationToken cancellationToken = default);
    
    Task<List<HrInterviewQuestionDto>> GetHrInterviewQuestionDtosAsync(
        HrInterviewQuestionSection? section = null, bool? isUsing = null, CancellationToken cancellationToken = default);

    Task AddHrInterviewQuestionsAsync(List<HrInterviewQuestion> questions, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateHrInterviewQuestionsAsync(List<HrInterviewQuestion> questions, bool forceSave = true, CancellationToken cancellationToken = default);
}

public class HrDataProvider : IHrDataProvider
{
    private readonly IMapper _mapper;
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public HrDataProvider(IRepository repository, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _mapper = mapper;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<HrInterviewQuestion>> GetHrInterviewQuestionsAsync(
        HrInterviewQuestionSection? section = null, bool? isUsing = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<HrInterviewQuestion>();

        if (section.HasValue)
            query = query.Where(x => x.Section == section.Value);
        

        if (isUsing.HasValue)
            query = query.Where(x => x.IsUsing == isUsing.Value);

        return await query.ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<HrInterviewQuestionDto>> GetHrInterviewQuestionDtosAsync(
        HrInterviewQuestionSection? section = null, bool? isUsing = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<HrInterviewQuestion>();

        if (section.HasValue)
            query = query.Where(x => x.Section == section.Value);
        

        if (isUsing.HasValue)
            query = query.Where(x => x.IsUsing == isUsing.Value);

        return await query.ProjectTo<HrInterviewQuestionDto>(_mapper.ConfigurationProvider).ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task AddHrInterviewQuestionsAsync(List<HrInterviewQuestion> questions, bool forceSave, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(questions, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateHrInterviewQuestionsAsync(List<HrInterviewQuestion> questions, bool forceSave, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(questions, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}