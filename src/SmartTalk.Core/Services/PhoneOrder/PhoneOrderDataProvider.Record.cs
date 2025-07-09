using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Core.Domain.System;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Dto.Restaurant;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderDataProvider
{
    Task AddPhoneOrderRecordsAsync(List<PhoneOrderRecord> phoneOrderRecords, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<List<PhoneOrderRecord>> GetPhoneOrderRecordsAsync(int? agentId, string name, CancellationToken cancellationToken);

    Task<List<PhoneOrderOrderItem>> AddPhoneOrderItemAsync(List<PhoneOrderOrderItem> phoneOrderOrderItems, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdatePhoneOrderRecordsAsync(PhoneOrderRecord record, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<List<PhoneOrderRecord>> GetPhoneOrderRecordAsync(int? recordId = null, DateTimeOffset? createdDate = null, CancellationToken cancellationToken = default);

    Task<PhoneOrderRecord> GetPhoneOrderRecordByTranscriptionJobIdAsync(string transcriptionJobId, CancellationToken cancellationToken = default);
    
    Task<List<GetPhoneOrderRecordsForRestaurantCountDto>> GetPhoneOrderRecordsForRestaurantCountAsync(
        DateTimeOffset dayShiftTime, DateTimeOffset nightShiftTime, DateTimeOffset endTime, CancellationToken cancellationToken);

    Task<List<GetPhoneOrderRecordsWithUserCountDto>> GetPhoneOrderRecordsWithUserCountAsync(
        DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken);
    
    Task<PhoneOrderRecord> GetPhoneOrderRecordByIdAsync(int recordId, CancellationToken cancellationToken);
    
    Task<PhoneOrderRecord> GetPhoneOrderRecordBySessionIdAsync(string sessionId, CancellationToken cancellationToken);
    
    Task<(PhoneOrderRecord, Agent, Domain.AISpeechAssistant.AiSpeechAssistant)> GetRecordWithAgentAndAssistantAsync(string sessionId, CancellationToken cancellationToken);
    
    Task<AiSpeechAssistantKnowledge> GetKnowledgePromptByAssistantIdAsync(int assistantId, CancellationToken cancellationToken);
    
    Task<List<PhoneOrderRecord>> GetPhoneOrderRecordsAsync(int? recordId = null, int? agentId = null, DateTimeOffset? createdDate = null, CancellationToken cancellationToken = default);
}

public partial class PhoneOrderDataProvider
{
    public async Task AddPhoneOrderRecordsAsync(List<PhoneOrderRecord> phoneOrderRecords, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        if (phoneOrderRecords == null || phoneOrderRecords.Count == 0) return;

        await _repository.InsertAllAsync(phoneOrderRecords, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PhoneOrderRecord>> GetPhoneOrderRecordsAsync(int? agentId, string name, CancellationToken cancellationToken)
    {
        var query = from record in _repository.Query<PhoneOrderRecord>()
            join agent in _repository.Query<Agent>() on record.AgentId equals agent.Id
            join assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>() on agent.Id equals assistant.AgentId
            where record.Status == PhoneOrderRecordStatus.Sent && (!agentId.HasValue || agent.Id == agentId.Value) && (string.IsNullOrEmpty(name) || assistant.Name.Contains(name))
            select record;
        
        return await query.Distinct().OrderByDescending(record => record.CreatedDate).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdatePhoneOrderRecordsAsync(PhoneOrderRecord record, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PhoneOrderRecord>> GetPhoneOrderRecordAsync(
        int? recordId = null, DateTimeOffset? createdDate = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<PhoneOrderRecord>();

        if (recordId.HasValue)
            query = query.Where(x => x.Id == recordId);

        if (createdDate.HasValue)
            query = query.Where(x => x.CreatedDate == createdDate && x.Status == PhoneOrderRecordStatus.Sent);
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PhoneOrderOrderItem>> AddPhoneOrderItemAsync(
        List<PhoneOrderOrderItem> phoneOrderOrderItems, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(phoneOrderOrderItems, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        
        return phoneOrderOrderItems;
    }
    
    public async Task<PhoneOrderRecord> GetPhoneOrderRecordByTranscriptionJobIdAsync(string transcriptionJobId, CancellationToken cancellationToken = default)
    {
        var query = from record in _repository.Query<PhoneOrderRecord>()
            join agent in _repository.Query<Agent>() on record.AgentId equals agent.Id into agentGroups
            from agent in agentGroups.DefaultIfEmpty()
            join restaurant in _repository.Query<Restaurant>() on agent.RelateId equals restaurant.Id into restaurantGroups
            from restaurant in restaurantGroups.DefaultIfEmpty()
            where record.TranscriptionJobId == transcriptionJobId
            select new { record, restaurant };

        var result = await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        result.record.RestaurantInfo = result.restaurant;
        
        return result.record;
    }

    public async Task<List<GetPhoneOrderRecordsForRestaurantCountDto>> GetPhoneOrderRecordsForRestaurantCountAsync(
        DateTimeOffset dayShiftTime, DateTimeOffset nightShiftTime, DateTimeOffset endTime, CancellationToken cancellationToken)
    {
        var query = from record in _repository.Query<PhoneOrderRecord>()
            join agent in _repository.Query<Agent>() on record.AgentId equals agent.Id into agentGroups
            from agent in agentGroups.DefaultIfEmpty()
            join restaurant in _repository.Query<Restaurant>() on agent.RelateId equals restaurant.Id into restaurantGroups
            from restaurant in restaurantGroups.DefaultIfEmpty()
            where record.Status == PhoneOrderRecordStatus.Sent
            select new { record, restaurant };

        var result = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        var records = result.Select(x =>
        {
            x.record.RestaurantInfo = x.restaurant;
            return x.record;
        });

        return records.GroupBy(x => x.RestaurantInfo.Id)
            .Select(restaurantGroup => new GetPhoneOrderRecordsForRestaurantCountDto
            {
                Restaurant = restaurantGroup.FirstOrDefault()?.RestaurantInfo != null ? _mapper.Map<RestaurantDto>(restaurantGroup.First().RestaurantInfo) : null,
                Classes = new List<RestaurantCountDto>
                {
                    new()
                    {
                        TimeFrame = "夜班",
                        Count = restaurantGroup.Count(x => x.CreatedDate >= dayShiftTime && x.CreatedDate <= nightShiftTime)
                    },
                    new()
                    {
                        TimeFrame = "日班",
                        Count = restaurantGroup.Count(x => x.CreatedDate >= nightShiftTime && x.CreatedDate < endTime)
                    }
                }
            }).ToList();
    }

    public async Task<List<GetPhoneOrderRecordsWithUserCountDto>> GetPhoneOrderRecordsWithUserCountAsync(
        DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneOrderRecord>()
            .Where(x => x.LastModifiedBy.HasValue)
            .Where(x => x.Status == PhoneOrderRecordStatus.Sent)
            .Where(x => x.LastModifiedDate >= startTime && x.LastModifiedDate < endTime)
            .Join(_repository.Query<UserAccount>(), x => x.LastModifiedBy, s => s.Id, (record, account) => new
            {
                UserName = account.UserName,
                record
            })
            .GroupBy(x => x.UserName)
            .Select(g => new GetPhoneOrderRecordsWithUserCountDto
            {
                UserName = g.Key,
                Count = g.Count()
            })
            .OrderBy(x => x.UserName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PhoneOrderRecord> GetPhoneOrderRecordByIdAsync(int recordId, CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneOrderRecord>().Where(x => x.Id == recordId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PhoneOrderRecord> GetPhoneOrderRecordBySessionIdAsync(string sessionId, CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneOrderRecord>().Where(x => x.SessionId == sessionId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<(PhoneOrderRecord, Agent, Domain.AISpeechAssistant.AiSpeechAssistant)> GetRecordWithAgentAndAssistantAsync(string sessionId, CancellationToken cancellationToken)
    {
        var result = await (
            from record in _repository.Query<PhoneOrderRecord>()
            where record.SessionId == sessionId
            join agent in _repository.Query<Agent>() on record.AgentId equals agent.Id into agentGroup
            from agent in agentGroup.DefaultIfEmpty()
            join assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>() on record.AgentId equals assistant.AgentId into assistantGroup
            from assistant in assistantGroup.DefaultIfEmpty()
            select new { record, agent, assistant }
        ).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return (result?.record, result?.agent, result?.assistant);
    }

    public async Task<AiSpeechAssistantKnowledge> GetKnowledgePromptByAssistantIdAsync(int assistantId, CancellationToken cancellationToken)
    {
        return await _repository.Query<AiSpeechAssistantKnowledge>().Where(x=> x.AssistantId == assistantId && x.IsActive).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PhoneOrderRecord>> GetPhoneOrderRecordsAsync(int? recordId = null, int? agentId = null, DateTimeOffset? createdDate = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<PhoneOrderRecord>();

        if (recordId.HasValue)
            query = query.Where(x => x.Id == recordId.Value);

        if (agentId.HasValue)
            query = query.Where(x => x.AgentId == agentId.Value);

        if (createdDate.HasValue)
            query = query.Where(x => x.CreatedDate == createdDate.Value);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}