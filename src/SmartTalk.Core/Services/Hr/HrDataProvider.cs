using SmartTalk.Core.Ioc;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Hr;
using SmartTalk.Messages.Enums.Hr;
using Microsoft.EntityFrameworkCore;

namespace SmartTalk.Core.Services.Hr;

public interface IHrDataProvider : IScopedDependency
{
    Task<List<HrInterviewQuestion>> GetHrInterviewQuestionsAsync(
        HrInterviewQuestionSection? section = null, bool? isUsing = null, CancellationToken cancellationToken = default);

    Task UpdateHrInterviewQuestionsAsync(List<HrInterviewQuestion> questions, bool forceSave = true, CancellationToken cancellationToken = default);
}

public class HrDataProvider : IHrDataProvider
{
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public HrDataProvider(IRepository repository, IUnitOfWork unitOfWork)
    {
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

    public async Task UpdateHrInterviewQuestionsAsync(List<HrInterviewQuestion> questions, bool forceSave, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(questions, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}