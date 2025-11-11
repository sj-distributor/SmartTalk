using System.Text.Json;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Data;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.AIAssistant;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Domain.System;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.Sales;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantDataProvider : IScopedDependency
{
    Task<(Domain.AISpeechAssistant.AiSpeechAssistant, AiSpeechAssistantKnowledge, AiSpeechAssistantUserProfile)>
        GetAiSpeechAssistantInfoByNumbersAsync(string callerNumber, string didNumber, int? assistantId = null, CancellationToken cancellationToken = default);
    
    Task<Domain.AISpeechAssistant.AiSpeechAssistant> GetAiSpeechAssistantByNumbersAsync(string didNumber, CancellationToken cancellationToken);

    Task<AiSpeechAssistantHumanContact> GetAiSpeechAssistantHumanContactByAssistantIdAsync(int assistantId, CancellationToken cancellationToken);
    
    Task<List<AiSpeechAssistantFunctionCall>> GetAiSpeechAssistantFunctionCallByAssistantIdAsync(
        int assistantId, AiSpeechAssistantProvider provider, bool? isActive = null, CancellationToken cancellationToken = default);

    Task<NumberPool> GetNumberAsync(int? numberId = null, bool? isUsed = null, CancellationToken cancellationToken = default);
    
    Task<List<NumberPool>> GetNumbersAsync(List<int> numberIds, CancellationToken cancellationToken);
    
    Task<(int, List<NumberPool>)> GetNumbersAsync(int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default);
    
    Task UpdateNumberPoolAsync(List<NumberPool> numbers, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<(int, List<Domain.AISpeechAssistant.AiSpeechAssistant>)> GetAiSpeechAssistantsAsync(
        int? pageIndex = null, int? pageSize = null, string channel = null, string keyword = null, List<int> agentIds = null, CancellationToken cancellationToken = default);

    Task AddAiSpeechAssistantsAsync(List<Domain.AISpeechAssistant.AiSpeechAssistant> assistants, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<AiSpeechAssistantKnowledge> GetAiSpeechAssistantKnowledgeAsync(int? assistantId = null, int? knowledgeId = null, bool? isActive = null, CancellationToken cancellationToken = default);

    Task AddAiSpeechAssistantKnowledgesAsync(List<AiSpeechAssistantKnowledge> knowledges, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAiSpeechAssistantKnowledgesAsync(List<AiSpeechAssistantKnowledge> knowledges, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<(int, List<AiSpeechAssistantKnowledge>)> GetAiSpeechAssistantKnowledgesAsync(int assistantId, int? pageIndex = null, int? pageSize = null, string version = null, CancellationToken cancellationToken = default);

    Task<List<Domain.AISpeechAssistant.AiSpeechAssistant>> DeleteAiSpeechAssistantsAsync(List<int> assistantIds, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<Domain.AISpeechAssistant.AiSpeechAssistant> GetAiSpeechAssistantAsync(int assistantId, CancellationToken cancellationToken);
    
    Task UpdateAiSpeechAssistantsAsync(List<Domain.AISpeechAssistant.AiSpeechAssistant> assistants, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<List<AiSpeechAssistantKnowledge>> GetAiSpeechAssistantActiveKnowledgesAsync(List<int> assistantIds, CancellationToken cancellationToken);
    
    Task<AiSpeechAssistantKnowledge> GetAiSpeechAssistantKnowledgeOrderByVersionAsync(int assistantId, CancellationToken cancellationToken);
    
    Task<Domain.AISpeechAssistant.AiSpeechAssistant> GetAiSpeechAssistantByIdAsync(int assistantId, CancellationToken cancellationToken);
    
    Task<int> GetMessageCountByAgentAndDateAsync(int groupKey, DateTimeOffset date, CancellationToken cancellationToken);
    
    Task AddAgentMessageRecordAsync(AgentMessageRecord messageRecord, CancellationToken cancellationToken = default);
    
    Task<AiKid> GetAiKidAsync(int? agentId = null, CancellationToken cancellationToken = default);
    
    Task AddAiKidAsync(AiKid kid, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<Domain.AISpeechAssistant.AiSpeechAssistant> GetAiSpeechAssistantWithKnowledgeAsync(int assistantId, CancellationToken cancellationToken);

    Task AddAiSpeechAssistantSessionAsync(AiSpeechAssistantSession session, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAiSpeechAssistantSessionAsync(AiSpeechAssistantSession session, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<AiSpeechAssistantSession> GetAiSpeechAssistantSessionBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<(Domain.AISpeechAssistant.AiSpeechAssistant Assistant, Agent Agent)> GetAiSpeechAssistantByAgentIdAsync(int agentId, CancellationToken cancellationToken);

    Task<Sales> GetCallInSalesByNameAsync(string assistantName, SalesCallType? type, CancellationToken cancellationToken);
    
    
    Task<List<AiSpeechAssistantInboundRoute>> GetAiSpeechAssistantInboundRouteAsync(string callerNumber, string didNumber, CancellationToken cancellationToken);
    
    Task<AiSpeechAssistantUserProfile> GetAiSpeechAssistantUserProfileAsync(int assistantId, string callerNumber, CancellationToken cancellationToken);
    
    Task AddAiSpeechAssistantFunctionCall(AiSpeechAssistantFunctionCall tool, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAiSpeechAssistantFunctionCall(List<AiSpeechAssistantFunctionCall> tools, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task AddAiSpeechAssistantHumanContactAsync(AiSpeechAssistantHumanContact humanContact, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAiSpeechAssistantHumanContactAsync(AiSpeechAssistantHumanContact humanContact, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<List<AiSpeechAssistantHumanContact>> GetAiSpeechAssistantHumanContactsAsync(List<int> assistantIds, CancellationToken cancellationToken);

    Task<List<CustomerItemsCache>> GetCustomerItemsCacheBySoldToIdsAsync(List<string> soldToIds, CancellationToken cancellationToken);
    
    Task UpsertCustomerItemsCacheAsync(string soldToId, string itemsString, bool forceSave, CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantDataProvider : IAiSpeechAssistantDataProvider
{
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AiSpeechAssistantDataProvider(IRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<(Domain.AISpeechAssistant.AiSpeechAssistant, AiSpeechAssistantKnowledge, AiSpeechAssistantUserProfile)>
        GetAiSpeechAssistantInfoByNumbersAsync(string callerNumber, string didNumber, int? assistantId = null, CancellationToken cancellationToken = default)
    {
        var assistantInfo =
            from assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>()
            join knowledge in _repository.Query<AiSpeechAssistantKnowledge>().Where(x => x.IsActive)
                on assistant.Id equals knowledge.AssistantId into knowledgeGroup
            from knowledge in knowledgeGroup.DefaultIfEmpty()
            join userProfile in _repository.Query<AiSpeechAssistantUserProfile>().Where(x => x.CallerNumber == callerNumber) 
                on assistant.Id equals userProfile.AssistantId into userProfileGroup
            from userProfile in userProfileGroup.DefaultIfEmpty()
            select new
            {
                assistant, knowledge, userProfile
            };

        assistantInfo = assistantInfo.Where(x => assistantId.HasValue ? x.assistant.Id == assistantId.Value : x.assistant.AnsweringNumber == didNumber);

        var result = await assistantInfo.FirstOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        return (result.assistant, result.knowledge, result.userProfile);
    }

    public async Task<Domain.AISpeechAssistant.AiSpeechAssistant> GetAiSpeechAssistantByNumbersAsync(string didNumber, CancellationToken cancellationToken)
    {
        var query = from assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>()
            join pool in _repository.Query<NumberPool>() on assistant.AnsweringNumberId equals pool.Id
            where pool.Number == didNumber
            select assistant;

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiSpeechAssistantHumanContact> GetAiSpeechAssistantHumanContactByAssistantIdAsync(int assistantId, CancellationToken cancellationToken)
    {
        return await _repository.Query<AiSpeechAssistantHumanContact>().Where(x => x.AssistantId == assistantId)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AiSpeechAssistantFunctionCall>> GetAiSpeechAssistantFunctionCallByAssistantIdAsync(
        int assistantId, AiSpeechAssistantProvider provider, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.QueryNoTracking<AiSpeechAssistantFunctionCall>().Where(x => x.AssistantId == assistantId && x.ModelProvider == provider);

        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value);
            
        return await query.ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<NumberPool> GetNumberAsync(int? numberId = null, bool? isUsed = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<NumberPool>();

        if (numberId.HasValue)
            query = query.Where(x => x.Id == numberId.Value);

        if (isUsed.HasValue)
            query = query.Where(x => x.IsUsed == isUsed.Value);
        
        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<NumberPool>> GetNumbersAsync(List<int> numberIds, CancellationToken cancellationToken)
    {
        return await _repository.Query<NumberPool>()
            .Where(x => numberIds.Contains(x.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(int, List<NumberPool>)> GetNumbersAsync(int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.QueryNoTracking<NumberPool>();

        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        if (pageIndex.HasValue && pageSize.HasValue)
            query = query.Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
        
        var numbers = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        return (count, numbers);
    }

    public async Task UpdateNumberPoolAsync(List<NumberPool> numbers, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(numbers, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(int, List<Domain.AISpeechAssistant.AiSpeechAssistant>)> GetAiSpeechAssistantsAsync(
        int? pageIndex = null, int? pageSize = null, string channel = null, string keyword = null, List<int> agentIds = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.QueryNoTracking<Domain.AISpeechAssistant.AiSpeechAssistant>().Where(x => x.IsDisplay);

        if (!string.IsNullOrEmpty(channel))
            query = query.Where(x => x.Channel.Contains(channel));
        
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(x => x.Name.Contains(keyword));

        if (agentIds != null && agentIds.Count != 0)
            query = query.Where(x => agentIds.Contains(x.AgentId));

        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        if (pageIndex.HasValue && pageSize.HasValue)
            query = query.Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
        
        var assistants = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        return (count, assistants);
    }

    public async Task AddAiSpeechAssistantsAsync(List<Domain.AISpeechAssistant.AiSpeechAssistant> assistants, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(assistants, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiSpeechAssistantKnowledge> GetAiSpeechAssistantKnowledgeAsync(
        int? assistantId = null, int? knowledgeId = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<AiSpeechAssistantKnowledge>();

        if (assistantId.HasValue)
            query = query.Where(x => x.AssistantId == assistantId.Value);

        if (knowledgeId.HasValue)
            query = query.Where(x => x.Id == knowledgeId.Value);

        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value);
        
        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAiSpeechAssistantKnowledgesAsync(List<AiSpeechAssistantKnowledge> knowledges, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(knowledges, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAiSpeechAssistantKnowledgesAsync(List<AiSpeechAssistantKnowledge> knowledges, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(knowledges, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(int, List<AiSpeechAssistantKnowledge>)> GetAiSpeechAssistantKnowledgesAsync(
        int assistantId, int? pageIndex = null, int? pageSize = null, string version = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<AiSpeechAssistantKnowledge>().Where(x => x.AssistantId == assistantId);

        if (!string.IsNullOrEmpty(version))
            query = query.Where(x => x.Version == version);

        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        
        if(pageIndex.HasValue && pageSize.HasValue)
            query = query.Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);

        var knowledges = await query.OrderByDescending(x => x.CreatedDate).ToListAsync(cancellationToken).ConfigureAwait(false);

        return (count, knowledges);
    }

    public async Task<List<Domain.AISpeechAssistant.AiSpeechAssistant>> DeleteAiSpeechAssistantsAsync(
        List<int> assistantIds, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        var assistants = await _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>()
            .Where(x => assistantIds.Contains(x.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);

        if (assistants.Count == 0) return [];

        await _repository.DeleteAllAsync(assistants, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        
        return assistants;
    }

    public async Task<Domain.AISpeechAssistant.AiSpeechAssistant> GetAiSpeechAssistantAsync(int assistantId, CancellationToken cancellationToken)
    {
        return await _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>()
            .Where(x => x.Id == assistantId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAiSpeechAssistantsAsync(List<Domain.AISpeechAssistant.AiSpeechAssistant> assistants, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(assistants, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AiSpeechAssistantKnowledge>> GetAiSpeechAssistantActiveKnowledgesAsync(List<int> assistantIds, CancellationToken cancellationToken)
    {
        return await _repository.Query<AiSpeechAssistantKnowledge>()
            .Where(x => assistantIds.Contains(x.AssistantId) && x.IsActive).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiSpeechAssistantKnowledge> GetAiSpeechAssistantKnowledgeOrderByVersionAsync(int assistantId, CancellationToken cancellationToken)
    {
        return await _repository.Query<AiSpeechAssistantKnowledge>()
            .Where(x => x.AssistantId == assistantId)
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Domain.AISpeechAssistant.AiSpeechAssistant> GetAiSpeechAssistantByIdAsync(int assistantId, CancellationToken cancellationToken)
    {
        return await _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>()
            .Where(x => x.Id == assistantId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> GetMessageCountByAgentAndDateAsync(int groupKey, DateTimeOffset date, CancellationToken cancellationToken)
    {
        return await _repository.Query<AgentMessageRecord>().Where(x => x.GroupKey == groupKey && x.MessageDate >= date)
            .CountAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAgentMessageRecordAsync(AgentMessageRecord messageRecord, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(messageRecord, cancellationToken).ConfigureAwait(false);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiKid> GetAiKidAsync(int? agentId = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<AiKid>();

        if (agentId.HasValue)
            query = query.Where(x => x.AgentId == agentId.Value);
        
        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAiKidAsync(AiKid kid, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(kid, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Domain.AISpeechAssistant.AiSpeechAssistant> GetAiSpeechAssistantWithKnowledgeAsync(int assistantId, CancellationToken cancellationToken)
    {
        var query = from a in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>()
            join k in _repository.Query<AiSpeechAssistantKnowledge>().Where(x => x.IsActive) on a.Id equals k.AssistantId into knowledgeGroup
            from k in knowledgeGroup.DefaultIfEmpty()
            where a.Id == assistantId
            select new { Assistant = a, Knowledge = k };
        
        var assistant = await query.Select(x => x.Assistant).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        
        if (assistant == null) return null;
            
        var knowledge = await query.Select(x => x.Knowledge).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        
        assistant.Knowledge = knowledge;
        return assistant;
    }

    public async Task AddAiSpeechAssistantSessionAsync(AiSpeechAssistantSession session, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(session, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAiSpeechAssistantSessionAsync(AiSpeechAssistantSession session, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiSpeechAssistantSession> GetAiSpeechAssistantSessionBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return await _repository.Query<AiSpeechAssistantSession>()
            .Where(x => x.SessionId == sessionId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<(Domain.AISpeechAssistant.AiSpeechAssistant Assistant, Agent Agent)> GetAiSpeechAssistantByAgentIdAsync(int agentId, CancellationToken cancellationToken)
    {
        var query = from agent in _repository.Query<Agent>()
            join assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>() on agent.Id equals assistant.AgentId into assistantGroups
            from assistant in assistantGroups.DefaultIfEmpty()
            where agent.Id == agentId
            select new { assistant, agent };

        var result = await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return (result?.assistant, result?.agent);
    }
    
    public async Task<List<AiSpeechAssistantInboundRoute>> GetAiSpeechAssistantInboundRouteAsync(string callerNumber,
        string didNumber, CancellationToken cancellationToken)
    {
        var routes = await _repository.Query<AiSpeechAssistantInboundRoute>()
            .Where(x => x.To == didNumber)
            .OrderBy(x => x.Priority)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var specifyCallerNumber = routes.Where(x => x.From == callerNumber).OrderBy(x => x.Priority).ToList();

        return specifyCallerNumber.Count != 0 ? specifyCallerNumber : routes;
    }

    public async Task<Sales> GetCallInSalesByNameAsync(string assistantName, SalesCallType? type,
        CancellationToken cancellationToken)
    {
        var query = _repository.Query<Sales>().Where(s => s.Name == assistantName);

        if (type.HasValue) query = query.Where(s => s.Type == type.Value);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AiSpeechAssistantUserProfile> GetAiSpeechAssistantUserProfileAsync(int assistantId, string callerNumber, CancellationToken cancellationToken)
    {
        var query = _repository.Query<AiSpeechAssistantUserProfile>()
            .Where(x => x.AssistantId == assistantId && x.CallerNumber == callerNumber);

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAiSpeechAssistantFunctionCall(AiSpeechAssistantFunctionCall tool, bool forceSave = true, CancellationToken cancellationToken = default)
    { 
        await _repository.InsertAsync(tool, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task UpdateAiSpeechAssistantFunctionCall(List<AiSpeechAssistantFunctionCall> tools, bool forceSave = true, CancellationToken cancellationToken = default)
    { 
        await _repository.UpdateAllAsync(tools, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAiSpeechAssistantHumanContactAsync(AiSpeechAssistantHumanContact humanContact, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(humanContact, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAiSpeechAssistantHumanContactAsync(AiSpeechAssistantHumanContact humanContact, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(humanContact, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AiSpeechAssistantHumanContact>> GetAiSpeechAssistantHumanContactsAsync(List<int> assistantIds, CancellationToken cancellationToken)
    {
        return await _repository.Query<AiSpeechAssistantHumanContact>().Where(x => assistantIds.Contains(x.AssistantId)).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<CustomerItemsCache>> GetCustomerItemsCacheBySoldToIdsAsync(List<string> soldToIds, CancellationToken cancellationToken)
    {
        return await _repository.Query<CustomerItemsCache>().Where(x => soldToIds.Contains(x.CacheKey)).ToListAsync(cancellationToken);
    }

    public async Task UpsertCustomerItemsCacheAsync(string soldToId, string itemsString, bool forceSave, CancellationToken cancellationToken)
    {
        var cache = await _repository.FirstOrDefaultAsync<CustomerItemsCache>(x => x.CacheKey == soldToId, cancellationToken);
        if (cache == null)
        {
            cache = new CustomerItemsCache
            {
                CacheKey = soldToId,
                CacheValue = itemsString,
                LastUpdated = DateTimeOffset.UtcNow
            };
            await _repository.InsertAsync(cache, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            cache.CacheValue = itemsString;
            cache.LastUpdated = DateTimeOffset.UtcNow;
            await _repository.UpdateAsync(cache, cancellationToken).ConfigureAwait(false);
        }
        
        if (forceSave)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}