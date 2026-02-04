using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.AutoTest;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestDataProvider : IScopedDependency
{
    Task AddAutoTestDataSetItemsAsync(List<AutoTestDataSetItem> setItems, CancellationToken cancellationToken);
    
    Task AddAutoTestDataSetAsync(AutoTestDataSet dataSet, CancellationToken cancellationToken);
    
    Task UpdateAutoTestDataSetAsync(AutoTestDataSet dataSet, bool forceSave = true, CancellationToken cancellationToken = default);

    Task AddAutoTestDataSetByQuoteAsync(List<AutoTestDataSetItem> items, CancellationToken cancellationToken);
    
    Task<(int count, List<AutoTestDataSet>)> GetAutoTestDataSetsAsync(int? page, int? pageSize, string? keyName, CancellationToken cancellationToken);

    Task<(int count, List<AutoTestDataItem>)> GetAutoTestDataItemsBySetIdAsync(int dataSetId, int? page, int? pageSize, CancellationToken cancellationToken);

    Task<AutoTestDataSet> GetAutoTestDataSetByIdAsync(int dataSetId, CancellationToken cancellationToken = default);

    Task DeleteAutoTestDataSetAsync(AutoTestDataSet dataSet, CancellationToken cancellationToken);
    
    Task<List<int>> GetDataItemIdsByDataSetIdAsync(int dataSetId, CancellationToken cancellationToken);

    Task DeleteAutoTestDataItemAsync(List<int> delIds, int dataSetId, CancellationToken cancellationToken);

    Task<AutoTestImportDataRecord> GetImportDataRecordsById(int id, CancellationToken cancellationToken);
    
    Task<List<AutoTestImportDataRecord>> GetImportDataRecordsByIdsAsync(List<int> ids, CancellationToken cancellationToken);
}

public partial class AutoTestDataProvider : IAutoTestDataProvider
{
    private readonly IMapper _mapper;
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    
    public AutoTestDataProvider(IRepository repository, IMapper mapper, IUnitOfWork unitOfWork, IAgentDataProvider agentDataProvider, IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _mapper = mapper;
        _unitOfWork = unitOfWork;
        _repository = repository;
        _agentDataProvider = agentDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
    }

    public async Task AddAutoTestDataSetItemsAsync(List<AutoTestDataSetItem> setItems, CancellationToken cancellationToken)
    {
        await _repository.InsertAllAsync(setItems, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task AddAutoTestDataSetAsync(AutoTestDataSet dataSet, CancellationToken cancellationToken)
    {
        await _repository.InsertAsync(dataSet, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAutoTestDataSetAsync(AutoTestDataSet dataSet, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(dataSet, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAutoTestDataSetByQuoteAsync(List<AutoTestDataSetItem> items, CancellationToken cancellationToken)
    {
        await _repository.InsertAllAsync(items, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<(int count, List<AutoTestDataSet>)> GetAutoTestDataSetsAsync(int? page, int? pageSize, string? keyName, CancellationToken cancellationToken)
    {
        var query = _repository.Query<AutoTestDataSet>().Where(x => x.IsDelete == false);

        if (!string.IsNullOrWhiteSpace(keyName))
        {
            query = query.Where(x => x.KeyName.Contains(keyName.Trim()));
        }

        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        if (page == null || pageSize == null)
        {
            var allData = await query
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return (count, allData);
        }

        var dataSets = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page.Value - 1) * pageSize.Value)
            .Take(pageSize.Value)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return (count, dataSets);
    }

    public async Task<(int count, List<AutoTestDataItem>)> GetAutoTestDataItemsBySetIdAsync(int dataSetId, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var query =
            from autoTestDataSetItem in _repository.Query<AutoTestDataSetItem>()
            join autoTestDataItem in _repository.Query<AutoTestDataItem>()
                on autoTestDataSetItem.DataItemId equals autoTestDataItem.Id
            where autoTestDataSetItem.DataSetId == dataSetId
            select autoTestDataItem;

        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        
        var allItems = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        
        var sortedItems = allItems.OrderByDescending(item =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(item.InputJson)) return DateTime.MinValue;

                var jsonObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(item.InputJson);

                if (jsonObject != null && jsonObject.TryGetValue("OrderDate", out var orderDateObj))
                {
                    if (DateTime.TryParse(orderDateObj?.ToString(), out var orderDate))
                    {
                        return orderDate;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error while extracting OrderDate from AutoTestDataItem {ItemId}", item.Id);
            }

            return DateTime.MinValue;
        });
        
        var sortedItemsList = sortedItems.ToList();
        
        List<AutoTestDataItem> resultItems;
        if (page.HasValue && pageSize.HasValue)
        {
            resultItems = sortedItemsList
                .Skip((page.Value - 1) * pageSize.Value)
                .Take(pageSize.Value)
                .ToList();
        }
        else
        {
            resultItems = sortedItemsList;
        }

        return (count, resultItems);
    }

    public async Task<List<int>> GetDataItemIdsByDataSetIdAsync(int dataSetId, CancellationToken cancellationToken)
    {
        return await _repository.Query<AutoTestDataSetItem>()
            .Where(x => x.DataSetId == dataSetId)
            .Select(x => x.DataItemId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
    
    public async Task<AutoTestDataSet> GetAutoTestDataSetByIdAsync(int dataSetId, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<AutoTestDataSet>()
            .FirstOrDefaultAsync(x => x.Id == dataSetId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteAutoTestDataSetAsync(AutoTestDataSet dataSet, CancellationToken cancellationToken)
    {
        dataSet.IsDelete = true;
        
        await _repository.UpdateAsync(dataSet, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    
    public async Task DeleteAutoTestDataItemAsync(List<int> delIds, int dataSetId, CancellationToken cancellationToken)
    {
        var deleteItems = await _repository.Query<AutoTestDataSetItem>()
            .Where(x => delIds.Contains(x.DataItemId) && x.DataSetId == dataSetId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        await _repository.DeleteAllAsync(deleteItems, cancellationToken).ConfigureAwait(false);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AutoTestImportDataRecord> GetImportDataRecordsById(int id, CancellationToken cancellationToken)
    {
        return await _repository.Query<AutoTestImportDataRecord>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<AutoTestImportDataRecord>> GetImportDataRecordsByIdsAsync(List<int> ids, CancellationToken cancellationToken)
    {
        return await _repository.QueryNoTracking<AutoTestImportDataRecord>()
            .Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}