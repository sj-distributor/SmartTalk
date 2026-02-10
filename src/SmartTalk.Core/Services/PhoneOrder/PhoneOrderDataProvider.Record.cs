using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.Printer;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Domain.System;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Dto.Restaurant;
using SmartTalk.Messages.Enums;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.Pos;
using SmartTalk.Messages.Enums.Sales;
using SmartTalk.Messages.Enums.STT;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderDataProvider
{
    Task AddPhoneOrderRecordsAsync(List<PhoneOrderRecord> phoneOrderRecords, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<List<PhoneOrderRecord>> GetPhoneOrderRecordsAsync(
        List<int> agentIds, string name, DateTimeOffset? utcStart = null, DateTimeOffset? utcEnd = null,
        List<DialogueScenarios> scenarios = null, int? assistantId = null, List<string> orderIds = null, CancellationToken cancellationToken = default);

    Task<List<PhoneOrderRecord>> GetPhoneOrderRecordsByAgentIdsAsync(List<int> agentIds, DateTimeOffset? utcStart = null, DateTimeOffset? utcEnd = null, CancellationToken cancellationToken = default);

    Task<List<PhoneOrderRecord>> GetPhoneOrderRecordsByAssistantIdsAsync(List<int> assistantIds, DateTimeOffset? utcStart = null, DateTimeOffset? utcEnd = null, CancellationToken cancellationToken = default);

    Task<Dictionary<int, PhoneOrderRecord>> GetLatestPhoneOrderRecordsByAssistantIdsAsync(
        List<int> assistantIds, int daysWindow, CancellationToken cancellationToken = default);
    
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
    
    Task<(PhoneOrderRecord, Agent)> GetRecordWithAgentAsync(string sessionId, CancellationToken cancellationToken);
    
    Task<(PhoneOrderRecord, Agent, Domain.AISpeechAssistant.AiSpeechAssistant)> GetRecordWithAgentAndAssistantAsync(string sessionId, CancellationToken cancellationToken);
    
    Task<AiSpeechAssistantKnowledge> GetKnowledgePromptByAssistantIdAsync(int assistantId, CancellationToken cancellationToken);
    
    Task<List<PhoneOrderRecord>> GetPhoneOrderRecordsAsync(
        int? recordId = null, int? agentId = null, DateTimeOffset? createdDate = null, DateTimeOffset? startTime = null, DateTimeOffset? endTime = null, CancellationToken cancellationToken = default);

    Task<List<(Domain.AISpeechAssistant.AiSpeechAssistant Assistant,PhoneOrderRecord Record)>> GetPhonCallUsagesAsync(
        DateTimeOffset startTime, DateTimeOffset endTime, bool includeExternalData = false, CancellationToken cancellationToken = default);

    Task AddPhoneOrderRecordReportsAsync(List<PhoneOrderRecordReport> recordReports, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<PhoneOrderRecordReport> GetPhoneOrderRecordReportAsync(string callSid, SystemLanguage language, CancellationToken cancellationToken);
    
    Task<List<PhoneOrderRecordReport>> GetPhoneOrderRecordReportByRecordIdAsync(List<int> recordId, CancellationToken cancellationToken);

    Task UpdatePhoneOrderRecordReportsAsync(List<PhoneOrderRecordReport> reports, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<PhoneOrderRecordScenarioHistory> AddPhoneOrderRecordScenarioHistoryAsync(PhoneOrderRecordScenarioHistory scenarioHistory, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<List<PhoneOrderRecordScenarioHistory>> GetPhoneOrderRecordScenarioHistoryAsync(int recordId, CancellationToken cancellationToken = default);
    
    Task<List<SimplePhoneOrderRecordDto>> GetSimplePhoneOrderRecordsByAgentIdsAsync(List<int> agentIds, CancellationToken cancellationToken);
    
    Task<List<SimplePhoneOrderRecordDto>> GetSimplePhoneOrderRecordsAsync(List<int> agentIds, CancellationToken cancellationToken);

    Task AddPhoneOrderReservationInformationAsync(PhoneOrderReservationInformation information, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<int?> GetLatestPhoneOrderRecordIdAsync(int agentId, int assistantId, string currentSessionId, CancellationToken cancellationToken);
    
    Task UpdateOrderIdAsync(int recordId, Guid orderId, CancellationToken cancellationToken);

    Task MarkRecordCompletedAsync(int recordId, CancellationToken cancellationToken = default);

    Task<List<int>> GetPhoneOrderReservationInfoUnreviewedRecordIdsAsync(List<int> recordIds, CancellationToken cancellationToken);
    
    Task< List<WaitingProcessingEventsDto>> GetWaitingProcessingEventsAsync(List<int> agentIds, WaitingTaskStatus? waitingTaskStatus = null, DateTimeOffset? utcStart = null, DateTimeOffset? utcEnd = null, List<TaskType> taskType = null, bool? isIncludeTodo = false, CancellationToken cancellationToken = default);

    Task AddWaitingProcessingEventAsync(WaitingProcessingEvent waitingProcessingEvent, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<string> GetRecordTaskSourceAsync(int recordId, CancellationToken cancellationToken = default);

    Task<List<WaitingProcessingEvent>> GetWaitingProcessingEventsAsync(List<int> ids = null, int? recordId = null, CancellationToken cancellationToken = default);

    Task DeleteWaitingProcessingEventAsync(WaitingProcessingEvent waitingProcessingEvent, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateWaitingProcessingEventsAsync(List<WaitingProcessingEvent> waitingProcessingEvents, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<(int, int)> GetAllOrUnreadWaitingProcessingEventsAsync(List<int> agentIds, List<TaskType> taskTypes = null, CancellationToken cancellationToken = default);

    Task<PhoneOrderRecordReport> GetOriginalPhoneOrderRecordReportAsync(int recordId, CancellationToken cancellationToken);

    Task<List<string>> GetTranscriptionTextsAsync(int assistantId, int recordId, DateTimeOffset utcStart, DateTimeOffset utcEnd, CancellationToken cancellationToken);
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
    
    public async Task<List<PhoneOrderRecord>> GetPhoneOrderRecordsAsync(
        List<int> agentIds, string name, DateTimeOffset? utcStart = null, DateTimeOffset? utcEnd = null, List<DialogueScenarios> scenarios = null, 
        int? assistantId = null, List<string> orderIds = null, CancellationToken cancellationToken = default)
    {
        var agentsQuery = from agent in _repository.Query<Agent>()
            join agentAssistant in _repository.Query<AgentAssistant>() on agent.Id equals agentAssistant.AgentId into agentAssistantGroups
            from agentAssistant in agentAssistantGroups.DefaultIfEmpty()
            join assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>() on agentAssistant.AssistantId equals assistant.Id into assistantGroups
            from assistant in assistantGroups.DefaultIfEmpty()
            where (agentIds == null || !agentIds.Any() || agentIds.Contains(agent.Id)) && (string.IsNullOrEmpty(name) || assistant == null || assistant.Name.Contains(name))
            select agent;

        Log.Information("GetPhoneOrderRecordsAsync: agentIds: {@agentIds}", agentIds);
        
        var agents = (await agentsQuery.ToListAsync(cancellationToken).ConfigureAwait(false)).Select(x => x.Id).Distinct().ToList();

        if (agents.Count == 0) return [];
        
        var query = from record in _repository.Query<PhoneOrderRecord>()
            where record.Status == PhoneOrderRecordStatus.Sent && agents.Contains(record.AgentId)
            select record;
        
        Log.Information("GetPhoneOrderRecordsAsync: recordCount: {@RecordCount}", query.Count());

        if (scenarios is { Count: > 0 })
        {
            var scenarioInts = scenarios.Select(s => (int)s).ToList();
            query = query.Where(r => r.Scenario.HasValue && scenarioInts.Contains((int)r.Scenario.Value));
        }
        
        if (utcStart.HasValue && utcEnd.HasValue)
            query = query.Where(record => record.CreatedDate >= utcStart.Value && record.CreatedDate < utcEnd.Value);
        
        if (assistantId.HasValue)
            query = query.Where(x => x.AssistantId.HasValue && x.AssistantId == assistantId.Value);

        if (orderIds != null && orderIds.Any()) 
        {
            if (orderIds.Count == 1) 
            { 
                var singleOrderId = orderIds[0]; 
                query = query.Where(r => r.OrderId != null && r.OrderId.Contains(singleOrderId)); 
            }
        }
        
        var records = await query.OrderByDescending(r => r.CreatedDate).Take(1000).ToListAsync(cancellationToken).ConfigureAwait(false);
        
        if (orderIds != null && orderIds.Count > 1)
            records = records.Where(r => r.OrderId != null && orderIds.Any(id => r.OrderId.Contains($"\"{id}\""))).ToList();

        return records;
    }

    public async Task<List<PhoneOrderRecord>> GetPhoneOrderRecordsByAgentIdsAsync(List<int> agentIds, DateTimeOffset? utcStart = null, DateTimeOffset? utcEnd = null, CancellationToken cancellationToken = default)
    {
        if (agentIds.Count == 0) return [];
        
        var query = from record in _repository.Query<PhoneOrderRecord>()
            where agentIds.Contains(record.AgentId)
            select record;

        if (utcStart.HasValue && utcEnd.HasValue)
            query = query.Where(record => record.CreatedDate >= utcStart.Value && record.CreatedDate < utcEnd.Value);

        return await query.OrderByDescending(record => record.CreatedDate).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PhoneOrderRecord>> GetPhoneOrderRecordsByAssistantIdsAsync(List<int> assistantIds, DateTimeOffset? utcStart = null, DateTimeOffset? utcEnd = null, CancellationToken cancellationToken = default)
    {
        if (assistantIds == null || assistantIds.Count == 0) return [];

        var query = _repository.Query<PhoneOrderRecord>()
            .Where(x => x.AssistantId.HasValue && assistantIds.Contains(x.AssistantId.Value))
            .Where(x => x.Status == PhoneOrderRecordStatus.Sent);

        if (utcStart.HasValue && utcEnd.HasValue)
            query = query.Where(record => record.CreatedDate >= utcStart.Value && record.CreatedDate < utcEnd.Value);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Dictionary<int, PhoneOrderRecord>> GetLatestPhoneOrderRecordsByAssistantIdsAsync(
        List<int> assistantIds, int daysWindow, CancellationToken cancellationToken = default)
    {
        if (assistantIds == null || assistantIds.Count == 0) return [];

        if (daysWindow <= 0) return [];

        var startUtc = DateTimeOffset.UtcNow.AddDays(-daysWindow);

        var records = await _repository.Query<PhoneOrderRecord>()
            .Where(x => x.AssistantId.HasValue && assistantIds.Contains(x.AssistantId.Value))
            .Where(x => x.Status == PhoneOrderRecordStatus.Sent)
            .Where(x => x.CreatedDate >= startUtc)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new Dictionary<int, PhoneOrderRecord>();

        foreach (var record in records)
        {
            var assistantId = record.AssistantId.GetValueOrDefault();
            if (result.ContainsKey(assistantId)) continue;

            result[assistantId] = record;
        }

        return result;
    }

    public async Task UpdatePhoneOrderRecordsAsync(PhoneOrderRecord record, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.Query<PhoneOrderRecord>()
            .Where(r => r.Id == record.Id)
            .Select(r => new { r.IsCompleted, r.OrderId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existing != null)
        {
            record.IsCompleted = existing.IsCompleted;
            record.OrderId = existing.OrderId;
        }
        
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
    
    public async Task<(PhoneOrderRecord, Agent)> GetRecordWithAgentAsync(string sessionId, CancellationToken cancellationToken)
    {
        var result = await (
            from record in _repository.Query<PhoneOrderRecord>() where record.SessionId == sessionId
            join agent in _repository.Query<Agent>() on record.AgentId equals agent.Id into agentGroup
            from agent in agentGroup.DefaultIfEmpty()
            select new { record, agent }
        ).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return (result?.record, result?.agent);
    }

    public async Task<(PhoneOrderRecord, Agent, Domain.AISpeechAssistant.AiSpeechAssistant)> GetRecordWithAgentAndAssistantAsync(string sessionId, CancellationToken cancellationToken)
    {
        var result = await (
            from record in _repository.Query<PhoneOrderRecord>().AsNoTracking()
            where record.SessionId == sessionId
            join agent in _repository.Query<Agent>() on record.AgentId equals agent.Id into agentGroup
            from agent in agentGroup.DefaultIfEmpty()
            join agentAssistant in _repository.Query<AgentAssistant>() on agent.Id equals agentAssistant.AgentId into agentAssistantGroups
            from agentAssistant in agentAssistantGroups.DefaultIfEmpty()
            join assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>().Where(x => x.IsDefault) on agentAssistant.AssistantId equals assistant.Id into assistantGroup
            from assistant in assistantGroup.DefaultIfEmpty()
            select new { record, agent, assistant }
        ).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return (result?.record, result?.agent, result?.assistant);
    }

    public async Task<AiSpeechAssistantKnowledge> GetKnowledgePromptByAssistantIdAsync(int assistantId, CancellationToken cancellationToken)
    {
        return await _repository.Query<AiSpeechAssistantKnowledge>().Where(x=> x.AssistantId == assistantId && x.IsActive).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PhoneOrderRecord>> GetPhoneOrderRecordsAsync(
        int? recordId = null, int? agentId = null, DateTimeOffset? createdDate = null, DateTimeOffset? startTime = null, DateTimeOffset? endTime = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<PhoneOrderRecord>();

        if (recordId.HasValue)
            query = query.Where(x => x.Id == recordId.Value);

        if (agentId.HasValue)
            query = query.Where(x => x.AgentId == agentId.Value);

        if (createdDate.HasValue)
            query = query.Where(x => x.CreatedDate == createdDate.Value);

        if (startTime.HasValue && endTime.HasValue)
            query = query.Where(x => x.CreatedDate >= startTime.Value && x.CreatedDate <= endTime.Value);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<(Domain.AISpeechAssistant.AiSpeechAssistant Assistant,PhoneOrderRecord Record)>> GetPhonCallUsagesAsync(
        DateTimeOffset startTime, DateTimeOffset endTime, bool includeExternalData = false, CancellationToken cancellationToken = default)
    {
        var query = from record in _repository.Query<PhoneOrderRecord>().Where(x => x.CreatedDate >= startTime && x.CreatedDate <= endTime)
            join agent in _repository.Query<Agent>().Where(x => !includeExternalData && x.Type != AgentType.AiKid) on record.AgentId equals agent.Id
            join agentAssistant in _repository.Query<AgentAssistant>() on agent.Id equals agentAssistant.AgentId
            join assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>().Where(x => x.IsDefault) on agentAssistant.AssistantId equals assistant.Id into assistantGroups
            from assistant in assistantGroups.DefaultIfEmpty()
            select new { assistant, record };
        
        var result = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        
        return result.Select(x => (x.assistant, x.record)).ToList();
    }

    public async Task AddPhoneOrderRecordReportsAsync(List<PhoneOrderRecordReport> recordReports, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        var recordId = recordReports.First().RecordId;
        
        var existingReports = await _repository.Query<PhoneOrderRecordReport>().Where(x => x.RecordId == recordId).ToListAsync(cancellationToken).ConfigureAwait(false);
        
        if (existingReports.Any())
        {
            await _repository.DeleteAllAsync(existingReports, cancellationToken).ConfigureAwait(false);
        }
        
        await _repository.InsertAllAsync(recordReports, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PhoneOrderRecordReport> GetPhoneOrderRecordReportAsync(string callSid, SystemLanguage language, CancellationToken cancellationToken)
    {
        var query = from record in _repository.Query<PhoneOrderRecord>().Where(x => x.SessionId == callSid)
            join report in _repository.Query<PhoneOrderRecordReport>() on record.Id equals report.RecordId
            where report.Language == (TranscriptionLanguage)language
            select report;

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<List<PhoneOrderRecordReport>> GetPhoneOrderRecordReportByRecordIdAsync(List<int> recordIds, CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneOrderRecordReport>()
            .Where(x => recordIds.Contains(x.RecordId))
            .GroupBy(x => x.RecordId)
            .Select(g => g.FirstOrDefault())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
    
    public async Task UpdatePhoneOrderRecordReportsAsync(List<PhoneOrderRecordReport> reports, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(reports, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<PhoneOrderRecordScenarioHistory> AddPhoneOrderRecordScenarioHistoryAsync(PhoneOrderRecordScenarioHistory scenarioHistory, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(scenarioHistory, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        
        return scenarioHistory;
    }
    
    public async Task<List<PhoneOrderRecordScenarioHistory>> GetPhoneOrderRecordScenarioHistoryAsync(int recordId, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<PhoneOrderRecordScenarioHistory>().Where(x => x.RecordId == recordId).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<SimplePhoneOrderRecordDto>> GetSimplePhoneOrderRecordsByAgentIdsAsync(List<int> agentIds, CancellationToken cancellationToken)
    {
        var query = from order in _repository.Query<PosOrder>().Where(x => x.RecordId.HasValue && x.Status == PosOrderStatus.Pending)
            join record in _repository.Query<PhoneOrderRecord>().Where(x => x.Status == PhoneOrderRecordStatus.Sent && x.AssistantId.HasValue && agentIds.Contains(x.AgentId)) on order.RecordId.Value equals record.Id
            join assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>() on record.AssistantId.Value equals assistant.Id
            select new SimplePhoneOrderRecordDto
            {
                Id = record.Id,
                AgentId = record.AgentId,
                AssistantId = record.AssistantId
            };
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<SimplePhoneOrderRecordDto>> GetSimplePhoneOrderRecordsAsync(List<int> agentIds, CancellationToken cancellationToken)
    {
        var printerOrders = _repository.Query<MerchPrinterOrder>();
        
        var query = from order in _repository.Query<PhoneOrderReservationInformation>()
            join record in _repository.Query<PhoneOrderRecord>().Where(x => x.Status == PhoneOrderRecordStatus.Sent && x.AssistantId.HasValue && agentIds.Contains(x.AgentId) && (x.Scenario == DialogueScenarios.Reservation || x.Scenario == DialogueScenarios.InformationNotification || x.Scenario == DialogueScenarios.ThirdPartyOrderNotification)) on order.RecordId equals record.Id
            where !printerOrders.Any(x => x.RecordId == order.RecordId)
            select new SimplePhoneOrderRecordDto
            {
                Id = record.Id,
                AgentId = record.AgentId,
                AssistantId = record.AssistantId,
                LastModifiedBy = order.LastModifiedBy
            };
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddPhoneOrderReservationInformationAsync(PhoneOrderReservationInformation information, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(information, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int?> GetLatestPhoneOrderRecordIdAsync(int agentId, int assistantId, string currentSessionId, CancellationToken cancellationToken)
    {
        var records = await _repository.Query<PhoneOrderRecord>().Where(r => r.AgentId == agentId && r.AssistantId == assistantId && r.SessionId != currentSessionId)
            .OrderByDescending(r => r.CreatedDate).ThenByDescending(r => r.Id).Select(r => r.Id).ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var recordId in records)
        {
            if (await IsRecordCompletedAsync(recordId, cancellationToken).ConfigureAwait(false))
                return recordId;
        }

        return null;
    }

    public async Task UpdateOrderIdAsync(int recordId, Guid orderId, CancellationToken cancellationToken)
    {
        var record = await _repository.Query<PhoneOrderRecord>().Where(r => r.Id == recordId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (record == null) return;

        var orderIds = string.IsNullOrEmpty(record.OrderId) ? new List<Guid>() : JsonConvert.DeserializeObject<List<Guid>>(record.OrderId)!;
        
        orderIds.Add(orderId); 
        record.OrderId = JsonConvert.SerializeObject(orderIds);

        await _repository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsRecordCompletedAsync(int recordId, CancellationToken cancellationToken)
    {
        var tasks = await _repository.Query<PhoneOrderPushTask>()
            .Where(t => t.RecordId == recordId)
            .Select(t => t.Status)
            .ToListAsync(cancellationToken);
        
        if (!tasks.Any())
            return true;
        
        return tasks.All(s => s == PhoneOrderPushTaskStatus.Sent);
    }

    public async Task MarkRecordCompletedAsync(int recordId, CancellationToken cancellationToken = default)
    {
        await _repository.Query<PhoneOrderRecord>().Where(r => r.Id == recordId && !r.IsCompleted)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.IsCompleted, true), cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<int>> GetPhoneOrderReservationInfoUnreviewedRecordIdsAsync(List<int> recordIds, CancellationToken cancellationToken)
    {
        return await (from info in _repository.QueryNoTracking<PhoneOrderReservationInformation>()
                join order in _repository.Query<MerchPrinterOrder>() on info.RecordId equals order.RecordId into oders
                from order in oders.DefaultIfEmpty()
                where recordIds.Contains(info.RecordId) && order == null
                select info.RecordId
            ).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PhoneOrderRecordReport> GetOriginalPhoneOrderRecordReportAsync(int recordId,
        CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneOrderRecordReport>().Where(x => x.RecordId == recordId && x.IsOrigin)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<List<WaitingProcessingEventsDto>> GetWaitingProcessingEventsAsync(List<int> agentIds, WaitingTaskStatus? waitingTaskStatus = null,
        DateTimeOffset? utcStart = null, DateTimeOffset? utcEnd = null, List<TaskType> taskType = null, bool? isIncludeTodo = false, CancellationToken cancellationToken = default)
    {
        var query = _repository.QueryNoTracking<WaitingProcessingEvent>().Where(x => agentIds.Contains(x.AgentId));

        if (waitingTaskStatus.HasValue)
            query = query.Where(x => x.TaskStatus == waitingTaskStatus.Value);
        
        if (utcStart.HasValue && utcEnd.HasValue)
            query = query.Where(x => x.CreatedDate >= utcStart.Value && x.CreatedDate < utcEnd.Value);

        var hasTodoTaskType = taskType is { Count: > 0 } && taskType.Contains(TaskType.Todo);
        var filterByIsIncludeTodoOnly = isIncludeTodo == true && hasTodoTaskType;

        if (taskType is { Count: > 0 } && !filterByIsIncludeTodoOnly)
            query = query.Where(x => taskType.Contains(x.TaskType));

        if (isIncludeTodo.HasValue)
            query = query.Where(x => x.IsIncludeTodo == true);
        
        return await (from events in query
            join record in _repository.QueryNoTracking<PhoneOrderRecord>() on events.RecordId equals record.Id
            join userAccount in _repository.QueryNoTracking<UserAccount>() on events.LastModifiedBy equals userAccount.Id into userAccounts
            from userAccount in userAccounts.DefaultIfEmpty()
            select new WaitingProcessingEventsDto
            {
                Id = events.Id,
                Url = record.Url,
                AgentId = events.AgentId,
                RecordId = events.RecordId,
                TaskType = events.TaskType,
                Scenario = record.Scenario,
                SessionId = record.SessionId,
                TaskStatus = events.TaskStatus,
                TaskSource = events.TaskSource,
                CreatedDate = events.CreatedDate,
                LastModifiedByName = userAccount.UserName
            }).OrderByDescending(x => x.CreatedDate).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddWaitingProcessingEventAsync(WaitingProcessingEvent waitingProcessingEvent, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(waitingProcessingEvent, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<string> GetRecordTaskSourceAsync(int recordId, CancellationToken cancellationToken = default)
    {
        var query = from record in _repository.Query<PhoneOrderRecord>() where record.Id == recordId
            join assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>() on record.AssistantId equals assistant.Id into assistantJoin
            from assistant in assistantJoin.DefaultIfEmpty()
            join agentAssistant in _repository.Query<AgentAssistant>() on record.AgentId equals agentAssistant.AgentId into agentAssistantJoin
            from agentAssistant in agentAssistantJoin.DefaultIfEmpty()
            join agent in _repository.Query<Agent>() on agentAssistant.AgentId equals agent.Id
            join posAgent in _repository.Query<PosAgent>() on agent.Id equals posAgent.AgentId
            join store in _repository.Query<CompanyStore>() on posAgent.StoreId equals store.Id
            join company in _repository.Query<Company>() on store.CompanyId equals company.Id
            select new 
            {
                RecordInfo = assistant != null 
                    ? $"{company.Name} - {store.Names} - {agent.Name} - {assistant.Name}" 
                    : $"{company.Name} - {store.Names} - {agent.Name}"
            };

        var results = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    
        return results.FirstOrDefault()?.RecordInfo ?? string.Empty;
    }

    public async Task<List<WaitingProcessingEvent>> GetWaitingProcessingEventsAsync(List<int> ids = null, int? recordId = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<WaitingProcessingEvent>();

        if (ids is { Count: > 0 })
            query = query.Where(x => ids.Contains(x.Id));
        
        if (recordId.HasValue)
            query = query.Where(x => x.RecordId == recordId);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteWaitingProcessingEventAsync(WaitingProcessingEvent waitingProcessingEvent, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(waitingProcessingEvent, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateWaitingProcessingEventsAsync(List<WaitingProcessingEvent> waitingProcessingEvents, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(waitingProcessingEvents, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(int, int)> GetAllOrUnreadWaitingProcessingEventsAsync(List<int> agentIds, List<TaskType> taskTypes = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.QueryNoTracking<WaitingProcessingEvent>().Where(x => agentIds.Contains(x.AgentId));

        if (taskTypes is { Count: > 0 })
            query = query.Where(x => taskTypes.Contains(x.TaskType));
        
        var result = await query.GroupBy(_ => 1)
            .Select(g => new
            {
                All = g.Count(),
                Unread = g.Count(x => x.TaskStatus == WaitingTaskStatus.Unfinished)
            }).SingleOrDefaultAsync(cancellationToken);

        return result == null ? (0, 0) : (result.All, result.Unread);
    }

    public async Task<List<string>> GetTranscriptionTextsAsync(int assistantId, int recordId, DateTimeOffset utcStart, DateTimeOffset utcEnd, CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneOrderRecord>().AsNoTracking()
            .Where(x =>
                x.AssistantId == assistantId &&
                x.Id != recordId &&
                x.CreatedDate >= utcStart &&
                x.CreatedDate < utcEnd &&
                !string.IsNullOrEmpty(x.TranscriptionText))
            .OrderBy(x => x.CreatedDate)
            .Select(x => x.TranscriptionText)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
