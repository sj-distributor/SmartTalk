using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.HrInterView;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.HrInterView;

public interface IHrInterViewDataProvider : IScopedDependency
{
    Task AddHrInterViewSettingAsync(HrInterViewSetting setting, bool forceSave = true, CancellationToken cancellationToken = default);
   
    Task UpdateHrInterViewSettingAsync(HrInterViewSetting setting, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<HrInterViewSetting> GetHrInterViewSettingByIdAsync(int settingId, CancellationToken cancellationToken);

    Task<(List<HrInterViewSetting>, int)> GetHrInterViewSettingsAsync(int? settingId, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default);
    
    Task AddHrInterViewSettingQuestionsAsync(List<HrInterViewSettingQuestion> questions, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateHrInterViewSettingQuestionsAsync(List<HrInterViewSettingQuestion> questions, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task DeleteHrInterViewSettingQuestionsAsync(List<HrInterViewSettingQuestion> questions, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<List<HrInterViewSettingQuestion>> GetHrInterViewSettingQuestionsByIdAsync (List<int> questionIds, CancellationToken cancellationToken);
    
    Task<List<HrInterViewSettingQuestion>> GetHrInterViewSettingQuestionsBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken);

    Task AddHrInterViewSessionAsync(HrInterViewSession session, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<(List<HrInterViewSession>, int)> GetHrInterViewSessionsAsync (Guid? sessionId, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default);
}

public class HrInterViewDataProvider : IHrInterViewDataProvider
{
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    
    public HrInterViewDataProvider(IRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task AddHrInterViewSettingAsync(HrInterViewSetting setting, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(setting, cancellationToken).ConfigureAwait(false);
        
        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateHrInterViewSettingAsync(HrInterViewSetting setting, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(setting, cancellationToken).ConfigureAwait(false);
        
        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<HrInterViewSetting> GetHrInterViewSettingByIdAsync(int settingId, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync<HrInterViewSetting>(settingId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<(List<HrInterViewSetting>, int)> GetHrInterViewSettingsAsync(int? settingId, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.QueryNoTracking<HrInterViewSetting>();

        if (settingId.HasValue)
            query = query.Where(x => x.Id == settingId);
        
        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        
        if (pageIndex.HasValue && pageSize.HasValue)
            query = query.Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
        
        return (await query.ToListAsync(cancellationToken).ConfigureAwait(false), count);
    }

    public async Task AddHrInterViewSettingQuestionsAsync(List<HrInterViewSettingQuestion> questions, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(questions, cancellationToken).ConfigureAwait(false);
        
        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateHrInterViewSettingQuestionsAsync(List<HrInterViewSettingQuestion> questions, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(questions, cancellationToken).ConfigureAwait(false);
        
        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteHrInterViewSettingQuestionsAsync(List<HrInterViewSettingQuestion> questions, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAllAsync(questions, cancellationToken).ConfigureAwait(false);
        
        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<HrInterViewSettingQuestion>> GetHrInterViewSettingQuestionsBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return await _repository.Query<HrInterViewSettingQuestion>().Where(x => sessionId == x.SessionId).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<HrInterViewSettingQuestion>> GetHrInterViewSettingQuestionsByIdAsync(List<int> questionIds, CancellationToken cancellationToken)
    {
        return await _repository.Query<HrInterViewSettingQuestion>().Where(x => questionIds.Contains(x.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddHrInterViewSessionAsync(HrInterViewSession session, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(session, cancellationToken).ConfigureAwait(false);
        
        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(List<HrInterViewSession>, int)> GetHrInterViewSessionsAsync(Guid? sessionId, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.QueryNoTracking<HrInterViewSession>();

        if (sessionId.HasValue)
            query = query.Where(x => x.SessionId == sessionId);
        
        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        
        if (pageIndex.HasValue && pageSize.HasValue)
            query = query.Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
        
        return (await query.ToListAsync(cancellationToken).ConfigureAwait(false), count);
    }
}