using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Data;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.AIAssistant;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Domain.System;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.Sales;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantDataProvider : IScopedDependency
{
    Task<(Domain.AISpeechAssistant.AiSpeechAssistant, AiSpeechAssistantKnowledge, AiSpeechAssistantUserProfile)>
        GetAiSpeechAssistantInfoByNumbersAsync(string callerNumber, string didNumber, int? assistantId = null, CancellationToken cancellationToken = default);
    
    Task<Domain.AISpeechAssistant.AiSpeechAssistant> GetAiSpeechAssistantByNumbersAsync(string didNumber, CancellationToken cancellationToken);

    Task<AiSpeechAssistantHumanContact> GetAiSpeechAssistantHumanContactByAssistantIdAsync(int assistantId, CancellationToken cancellationToken);
    
    Task<List<AiSpeechAssistantFunctionCall>> GetAiSpeechAssistantFunctionCallByAssistantIdsAsync(
        List<int> assistantIds, AiSpeechAssistantProvider provider, bool? isActive = null, CancellationToken cancellationToken = default);

    Task<NumberPool> GetNumberAsync(int? numberId = null, bool? isUsed = null, CancellationToken cancellationToken = default);
    
    Task<List<NumberPool>> GetNumbersAsync(List<int> numberIds, CancellationToken cancellationToken);
    
    Task<(int, List<NumberPool>)> GetNumbersAsync(int? pageIndex = null, int? pageSize = null, CancellationToken cancellationToken = default);
    
    Task UpdateNumberPoolAsync(List<NumberPool> numbers, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<(int, List<Domain.AISpeechAssistant.AiSpeechAssistant>)> GetAiSpeechAssistantsAsync(
        int? pageIndex = null, int? pageSize = null, string channel = null, string keyword = null, List<int> agentIds = null, bool? isDefault = null, CancellationToken cancellationToken = default);

    Task AddAiSpeechAssistantsAsync(List<Domain.AISpeechAssistant.AiSpeechAssistant> assistants, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<AiSpeechAssistantKnowledge> GetAiSpeechAssistantKnowledgeAsync(int? assistantId = null, int? knowledgeId = null, bool? isActive = null, CancellationToken cancellationToken = default);

    Task AddAiSpeechAssistantKnowledgesAsync(List<AiSpeechAssistantKnowledge> knowledges, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAiSpeechAssistantKnowledgesAsync(List<AiSpeechAssistantKnowledge> knowledges, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<(int, List<AiSpeechAssistantKnowledge>)> GetAiSpeechAssistantKnowledgesAsync(int assistantId, int? pageIndex = null, int? pageSize = null, string version = null, CancellationToken cancellationToken = default);

    Task<List<Domain.AISpeechAssistant.AiSpeechAssistant>> DeleteAiSpeechAssistantByIdsAsync(List<int> assistantIds, bool forceSave = true, CancellationToken cancellationToken = default);

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

    Task<(Domain.AISpeechAssistant.AiSpeechAssistant Assistant, Agent Agent)> GetAgentAndAiSpeechAssistantAsync(int agentId, int? assistantId, CancellationToken cancellationToken);
    
    Task<List<AiSpeechAssistantInboundRoute>> GetAiSpeechAssistantInboundRouteAsync(string callerNumber, string didNumber, CancellationToken cancellationToken);
    
    Task<AiSpeechAssistantUserProfile> GetAiSpeechAssistantUserProfileAsync(int assistantId, string callerNumber, CancellationToken cancellationToken);
    
    Task AddAiSpeechAssistantFunctionCallsAsync(List<AiSpeechAssistantFunctionCall> tools, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAiSpeechAssistantFunctionCallAsync(List<AiSpeechAssistantFunctionCall> tools, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task AddAiSpeechAssistantHumanContactAsync(List<AiSpeechAssistantHumanContact> humanContacts, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAiSpeechAssistantHumanContactsAsync(List<AiSpeechAssistantHumanContact> humanContacts, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<List<AiSpeechAssistantHumanContact>> GetAiSpeechAssistantHumanContactsAsync(List<int> assistantIds, CancellationToken cancellationToken);
    
    Task AddAgentAssistantsAsync(List<AgentAssistant> agentAssistants, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<List<AgentAssistant>> GetAgentAssistantsAsync(List<int> agentIds = null, List<int> assistantIds = null, CancellationToken cancellationToken = default);
    
    Task<List<(Agent, AgentAssistant)>> GetAgentWithAssistantsAsync(List<int> assistantIds = null, CancellationToken cancellationToken = default);
    
    Task DeleteAgentAssistantsAsync(List<AgentAssistant> agentAssistants, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<(List<AgentAssistant>, List<Domain.AISpeechAssistant.AiSpeechAssistant>)> GetAgentAssistantWithAssistantsAsync(int agentId, CancellationToken cancellationToken = default);
    
    Task DeleteAiSpeechAssistantsAsync(List<Domain.AISpeechAssistant.AiSpeechAssistant> assistants, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<List<Domain.AISpeechAssistant.AiSpeechAssistant>> GetAiSpeechAssistantByIdsAsync(List<int> assistantIds, CancellationToken cancellationToken = default);
    
    Task AddAiSpeechAssistantInboundRouteAsync(List<AiSpeechAssistantInboundRoute> routes, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateAiSpeechAssistantInboundRouteAsync(List<AiSpeechAssistantInboundRoute> routes, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<AiSpeechAssistantInboundRoute> GetAiSpeechAssistantInboundRouteByIdAsync(int routeId, CancellationToken cancellationToken = default);
    
    Task<List<AiSpeechAssistantInboundRoute>> DeleteAiSpeechAssistantInboundRoutesAsync(List<int> routeIds, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<(int, List<AiSpeechAssistantInboundRoute>)> GetAiSpeechAssistantInboundRoutesAsync(int pageIndex, int pageSize, int assistantId, string keyword = null, CancellationToken cancellationToken = default);
    
    Task<List<(AgentAssistant, Domain.AISpeechAssistant.AiSpeechAssistant)>> GetAiSpeechAssistantsByAgentIdsAsync(List<int> agentIds, CancellationToken cancellationToken = default);
    
    Task<Domain.AISpeechAssistant.AiSpeechAssistant> GetAiSpeechAssistantByAgentIdAsync(int agentId, CancellationToken cancellationToken);
    
    Task<List<Domain.AISpeechAssistant.AiSpeechAssistant>> GetAiSpeechAssistantsByAgentIdAsync(int agentId, CancellationToken cancellationToken);
    
    Task<List<AiSpeechAssistantInboundRoute>> GetAiSpeechAssistantInboundRoutesByAgentIdAsync(int agentId, CancellationToken cancellationToken = default);
    
    Task DeleteAiSpeechAssistantFunctionCallsAsync(List<AiSpeechAssistantFunctionCall> tools, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task DeleteAiSpeechAssistantHumanContactsAsync(List<AiSpeechAssistantHumanContact> humanContacts, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<List<(Agent, Domain.AISpeechAssistant.AiSpeechAssistant)>> GetAgentAndAiSpeechAssistantPairsAsync(CancellationToken cancellationToken);
    
    Task<List<Domain.AISpeechAssistant.AiSpeechAssistant>> GetAiSpeechAssistantsByStoreIdAsync(int storeId, CancellationToken cancellationToken = default);
}

public partial class AiSpeechAssistantDataProvider : IAiSpeechAssistantDataProvider
{
    private readonly IMapper _mapper;
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AiSpeechAssistantDataProvider(IRepository repository, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _mapper = mapper;
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

    public async Task<List<AiSpeechAssistantFunctionCall>> GetAiSpeechAssistantFunctionCallByAssistantIdsAsync(
        List<int> assistantIds, AiSpeechAssistantProvider provider, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.QueryNoTracking<AiSpeechAssistantFunctionCall>().Where(x => assistantIds.Contains(x.AssistantId) && x.ModelProvider == provider);

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
        int? pageIndex = null, int? pageSize = null, string channel = null, string keyword = null, List<int> agentIds = null, bool? isDefault = null,           CancellationToken cancellationToken = default)
    {
        var query = from agentAssistant in _repository.QueryNoTracking<AgentAssistant>()
            join assistant in _repository.QueryNoTracking<Domain.AISpeechAssistant.AiSpeechAssistant>()
                .Where(x => x.IsDisplay) on agentAssistant.AssistantId equals assistant.Id
            select new { agentAssistant, assistant };

        if (!string.IsNullOrEmpty(channel))
            query = query.Where(x => x.assistant.Channel.Contains(channel));
        
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(x => x.assistant.Name.Contains(keyword));

        if (agentIds != null && agentIds.Count != 0)
            query = query.Where(x => agentIds.Contains(x.agentAssistant.AgentId));
        
        if (isDefault.HasValue)
            query = query.Where(x => x.assistant.IsDefault == isDefault.Value);

        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        if (pageIndex.HasValue && pageSize.HasValue)
            query = query.Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
        
        var assistants = await query.Select(x => x.assistant).OrderByDescending(x => x.CreatedDate).ToListAsync(cancellationToken).ConfigureAwait(false);

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

    public async Task<List<Domain.AISpeechAssistant.AiSpeechAssistant>> DeleteAiSpeechAssistantByIdsAsync(
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
    
    public async Task<(Domain.AISpeechAssistant.AiSpeechAssistant Assistant, Agent Agent)> GetAgentAndAiSpeechAssistantAsync(int agentId, int? assistantId, CancellationToken cancellationToken)
    {
        var query = from agent in _repository.Query<Agent>()
            join agentAssistant in _repository.Query<AgentAssistant>() on agent.Id equals agentAssistant.AgentId
            join assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>() on agentAssistant.AssistantId equals assistant.Id into assistantGroups
            from assistant in assistantGroups.DefaultIfEmpty()
            where agent.Id == agentId
            select new { assistant, agent };
        
        if (assistantId.HasValue)
        {
            query = query.Where(x => x.assistant != null && x.assistant.Id == assistantId.Value);
        }
        else
        {
            query = query.Where(x => x.assistant != null && x.assistant.IsDefault);
        }

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

        return specifyCallerNumber.Count != 0 ? specifyCallerNumber : routes.Where(x => x.IsFallback).OrderBy(x => x.Priority).ToList();
    }

    public async Task<AiSpeechAssistantUserProfile> GetAiSpeechAssistantUserProfileAsync(int assistantId, string callerNumber, CancellationToken cancellationToken)
    {
        var query = _repository.Query<AiSpeechAssistantUserProfile>()
            .Where(x => x.AssistantId == assistantId && x.CallerNumber == callerNumber);

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAiSpeechAssistantFunctionCallsAsync(List<AiSpeechAssistantFunctionCall> tools, bool forceSave = true, CancellationToken cancellationToken = default)
    { 
        await _repository.InsertAllAsync(tools, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task UpdateAiSpeechAssistantFunctionCallAsync(List<AiSpeechAssistantFunctionCall> tools, bool forceSave = true, CancellationToken cancellationToken = default)
    { 
        await _repository.UpdateAllAsync(tools, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAiSpeechAssistantHumanContactAsync(List<AiSpeechAssistantHumanContact> humanContacts, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(humanContacts, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAiSpeechAssistantHumanContactsAsync(List<AiSpeechAssistantHumanContact> humanContacts, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(humanContacts, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AiSpeechAssistantHumanContact>> GetAiSpeechAssistantHumanContactsAsync(List<int> assistantIds, CancellationToken cancellationToken)
    {
        return await _repository.Query<AiSpeechAssistantHumanContact>().Where(x => assistantIds.Contains(x.AssistantId)).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task AddAgentAssistantsAsync(List<AgentAssistant> agentAssistants, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(agentAssistants, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AgentAssistant>> GetAgentAssistantsAsync(List<int> agentIds = null, List<int> assistantIds = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<AgentAssistant>();

        if (agentIds != null && agentIds.Count != 0)
            query = query.Where(x => agentIds.Contains(x.AgentId));

        if (assistantIds != null && assistantIds.Count != 0)
            query = query.Where(x => assistantIds.Contains(x.AssistantId));
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<(Agent, AgentAssistant)>> GetAgentWithAssistantsAsync(List<int> assistantIds = null, CancellationToken cancellationToken = default)
    {
        var query = from agent in _repository.Query<Agent>()
            join agentAssistant in _repository.Query<AgentAssistant>() on agent.Id equals agentAssistant.AgentId into agentAssistants
            from agentAssistant in agentAssistants.DefaultIfEmpty()
            where assistantIds.Contains(agentAssistant.AssistantId)
            select new { agent, agentAssistant };
        
        var result = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        
        return result.Select(x => (x.agent, x.agentAssistant)).ToList();
    }

    public async Task DeleteAgentAssistantsAsync(List<AgentAssistant> agentAssistants, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAllAsync(agentAssistants, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(List<AgentAssistant>, List<Domain.AISpeechAssistant.AiSpeechAssistant>)> GetAgentAssistantWithAssistantsAsync(int agentId, CancellationToken cancellationToken = default)
    {
        var query = from agentAssistant in _repository.Query<AgentAssistant>()
            join assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>() on agentAssistant.AssistantId equals assistant.Id into assistantGroups
            from assistant in assistantGroups.DefaultIfEmpty()
            where agentAssistant.AgentId == agentId
            select new { agentAssistant, assistant };

        var result = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        return (result.Select(x => x.agentAssistant).ToList(), result.Select(x => x.assistant).ToList());
    }

    public async Task DeleteAiSpeechAssistantsAsync(List<Domain.AISpeechAssistant.AiSpeechAssistant> assistants, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAllAsync(assistants, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<Domain.AISpeechAssistant.AiSpeechAssistant>> GetAiSpeechAssistantByIdsAsync(List<int> assistantIds, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>().Where(x => assistantIds.Contains(x.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAiSpeechAssistantInboundRouteAsync(List<AiSpeechAssistantInboundRoute> routes, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(routes, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAiSpeechAssistantInboundRouteAsync(List<AiSpeechAssistantInboundRoute> routes, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(routes, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiSpeechAssistantInboundRoute> GetAiSpeechAssistantInboundRouteByIdAsync(int routeId, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<AiSpeechAssistantInboundRoute>().Where(x => x.Id == routeId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AiSpeechAssistantInboundRoute>> DeleteAiSpeechAssistantInboundRoutesAsync(List<int> routeIds, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        var routes = await _repository.Query<AiSpeechAssistantInboundRoute>().Where(x => routeIds.Contains(x.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
        
        await _repository.DeleteAllAsync(routes, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        
        return routes;
    }

    public async Task<(int, List<AiSpeechAssistantInboundRoute>)> GetAiSpeechAssistantInboundRoutesAsync(
        int pageIndex, int pageSize, int assistantId, string keyword = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<AiSpeechAssistantInboundRoute>().Where(x => x.ForwardAssistantId == assistantId);

        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(x => x.From.Contains(keyword));
        
        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        
        var routes = await query.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        
        return (count, routes);
    }

    public async Task<List<(AgentAssistant, Domain.AISpeechAssistant.AiSpeechAssistant)>> GetAiSpeechAssistantsByAgentIdsAsync(List<int> agentIds, CancellationToken cancellationToken = default)
    {
        var query = from agentAssistant in _repository.Query<AgentAssistant>()
            join assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>() on agentAssistant.AssistantId equals assistant.Id
            where agentIds.Contains(agentAssistant.AgentId) && assistant.IsDefault
            select new { agentAssistant, assistant };
        
        var result = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        
        return result.Select(x => (x.agentAssistant, x.assistant)).ToList();
    }

    public async Task<Domain.AISpeechAssistant.AiSpeechAssistant> GetAiSpeechAssistantByAgentIdAsync(int agentId, CancellationToken cancellationToken)
    {
        var query = from agentAssistant in _repository.Query<AgentAssistant>()
            join assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>() on agentAssistant.AssistantId equals assistant.Id
            where agentAssistant.AgentId == agentId && assistant.IsDefault
            select assistant;
        
        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<Domain.AISpeechAssistant.AiSpeechAssistant>> GetAiSpeechAssistantsByAgentIdAsync(int agentId, CancellationToken cancellationToken)
    {
        var query = from agentAssistant in _repository.Query<AgentAssistant>()
            join assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>() on agentAssistant.AssistantId equals assistant.Id
            where agentAssistant.AgentId == agentId
            select assistant;
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AiSpeechAssistantInboundRoute>> GetAiSpeechAssistantInboundRoutesByAgentIdAsync(int agentId, CancellationToken cancellationToken = default)
    {
        var query = from agentAssistant in _repository.Query<AgentAssistant>()
            join assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>() on agentAssistant.AssistantId equals assistant.Id
            join route in _repository.Query<AiSpeechAssistantInboundRoute>() on assistant.Id equals route.ForwardAssistantId
            where agentAssistant.AgentId == agentId
            select route;
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAiSpeechAssistantFunctionCallsAsync(List<AiSpeechAssistantFunctionCall> tools, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAllAsync(tools, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAiSpeechAssistantHumanContactsAsync(List<AiSpeechAssistantHumanContact> humanContacts, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAllAsync(humanContacts, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<(Agent, Domain.AISpeechAssistant.AiSpeechAssistant)>> GetAgentAndAiSpeechAssistantPairsAsync(CancellationToken cancellationToken)
    {
        var query = from agent in _repository.Query<Agent>().Where(x => x.Type == AgentType.Assistant)
            join agentAssistant in _repository.Query<AgentAssistant>() on agent.Id equals agentAssistant.AgentId
            join assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>() on agentAssistant.AssistantId equals assistant.Id
            select new { agent, assistant };
        
        var result = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        
        return result.Select(x => (x.agent, x.assistant)).ToList();
    }

    public async Task<List<Domain.AISpeechAssistant.AiSpeechAssistant>> GetAiSpeechAssistantsByStoreIdAsync(int storeId, CancellationToken cancellationToken = default)
    {
        var query = from store in _repository.Query<CompanyStore>().Where(x => x.Id == storeId)
            join posAgent in _repository.Query<PosAgent>() on store.Id equals posAgent.StoreId
            join agentAssistant in _repository.Query<AgentAssistant>() on posAgent.AgentId equals agentAssistant.AgentId
            join assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>() on agentAssistant.AssistantId equals assistant.Id
            select assistant;
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}