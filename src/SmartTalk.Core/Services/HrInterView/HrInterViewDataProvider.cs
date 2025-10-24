using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.HrInterView;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.HrInterView;
using SmartTalk.Messages.Requests.HrInterView;

namespace SmartTalk.Core.Services.HrInterView;

public interface IHrInterViewDataProvider : IScopedDependency
{
    Task AddHrInterViewSettingAsync(HrInterViewSetting setting, bool forceSave = true, CancellationToken cancellationToken = default);
   
    Task UpdateHrInterViewSettingAsync(HrInterViewSetting setting, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<HrInterViewSetting> GetHrInterViewSettingByIdAsync(int settingId, CancellationToken cancellationToken);
    
    Task<HrInterViewSetting> GetHrInterViewSettingBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<(List<HrInterViewSettingDto>, int)> GetHrInterViewSettingsAsync(int? settingId, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default);
    
    Task AddHrInterViewSettingQuestionsAsync(List<HrInterViewSettingQuestion> questions, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateHrInterViewSettingQuestionsAsync(List<HrInterViewSettingQuestion> questions, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task DeleteHrInterViewSettingQuestionsAsync(List<HrInterViewSettingQuestion> questions, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<List<HrInterViewSettingQuestion>> GetHrInterViewSettingQuestionsByIdAsync (List<int> questionIds, CancellationToken cancellationToken);
    
    Task<List<HrInterViewSettingQuestion>> GetHrInterViewSettingQuestionsBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken);

    Task AddHrInterViewSessionAsync(HrInterViewSession session, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateHrInterViewSessionsAsync(List<HrInterViewSession> session, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<(List<HrInterViewSessionGroupDto>, int)> GetHrInterViewSessionsAsync(Guid? sessionId, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default);
   
    Task<List<HrInterViewSession>> GetHrInterViewSessionsBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

public class HrInterViewDataProvider : IHrInterViewDataProvider
{
    private readonly IMapper _mapper;
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    
    public HrInterViewDataProvider(IMapper mapper, IRepository repository, IUnitOfWork unitOfWork)
    {
        _mapper = mapper;
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

    public async Task<HrInterViewSetting> GetHrInterViewSettingBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return await _repository.Query<HrInterViewSetting>().Where(x => x.SessionId == sessionId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(List<HrInterViewSettingDto>, int)> GetHrInterViewSettingsAsync(int? settingId, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default)
    {
        var query = from setting in _repository.QueryNoTracking<HrInterViewSetting>()
                join question in _repository.QueryNoTracking<HrInterViewSettingQuestion>() 
                    on setting.Id equals question.SettingId into questions 
                from question in questions.DefaultIfEmpty()
                group question by setting into grouped 
                select new HrInterViewSettingDto
                {
                    Id = grouped.Key.Id,
                    Welcome = grouped.Key.Welcome,
                    EndMessage = grouped.Key.EndMessage,
                    SessionId = grouped.Key.SessionId,
                    CreatedDate = grouped.Key.CreatedDate,
                    Questions = grouped.Select(x => _mapper.Map<HrInterViewSettingQuestionDto>(x)).ToList()
                };

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

    public async Task UpdateHrInterViewSessionsAsync(List<HrInterViewSession> session, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(session, cancellationToken).ConfigureAwait(false);
        
        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(List<HrInterViewSessionGroupDto>, int)> GetHrInterViewSessionsAsync(Guid? sessionId, int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default)
    {
        var query = from  setting in _repository.QueryNoTracking<HrInterViewSetting>().Where(x => x.IsConvertText == true)
            join session in  _repository.QueryNoTracking<HrInterViewSession>()
                on setting.SessionId equals session.SessionId
                select session;

        var groupedQuery = query
            .GroupBy(x => x.SessionId)
            .Select(g => new 
            {
                SessionId = g.Key,
                Sessions = g.OrderBy(x => x.CreatedDate).Select(x => new HrInterViewSessionDto
                    {
                        Id = x.Id,
                        SessionId = x.SessionId,
                        Message = x.Message,
                        FileUrl = x.FileUrl,
                        QuestionType = x.QuestionType,
                        CreatedDate = x.CreatedDate
                    }).ToList(),
                FirstCreatedDate = g.Min(x => x.CreatedDate)
            });
        
        if (sessionId.HasValue)
            groupedQuery = groupedQuery.Where(x => x.SessionId == sessionId);
        
        groupedQuery = groupedQuery.OrderByDescending(x => x.FirstCreatedDate);
        
        var totalCount = await groupedQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        if (pageIndex.HasValue && pageSize.HasValue)
            groupedQuery = groupedQuery.Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);

        var result = await groupedQuery
            .Select(g => new HrInterViewSessionGroupDto
            {
                SessionId = g.SessionId,
                Sessions = g.Sessions
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
            
        return (result, totalCount);
    }

    public async Task<List<HrInterViewSession>> GetHrInterViewSessionsBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<HrInterViewSession>().Where(x => sessionId == x.SessionId).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}