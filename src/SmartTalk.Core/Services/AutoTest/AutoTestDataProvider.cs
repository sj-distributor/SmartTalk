using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.AutoTest;
using SmartTalk.Core.Services.Agents;

namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestDataProvider : IScopedDependency
{
    Task AddAutoTestDataItemsAsync(List<AutoTestDataSetItem> setItems, CancellationToken cancellationToken);
    
    Task AddAutoTestDataSetAsync(AutoTestDataSet dataSet, CancellationToken cancellationToken);

    Task AddAutoTestDataSetByQuoteAsync(List<AutoTestDataSetItem> items, CancellationToken cancellationToken);
    
    Task<(int count, List<AutoTestDataSet>)> GetAutoTestDataSetsAsync(int? page, int? pageSize, string? keyName, CancellationToken cancellationToken);

    Task<(int count, List<AutoTestDataItem>)> GetAutoTestDataItemsBySetIdAsync(int dataSetId, int? page, int? pageSize, CancellationToken cancellationToken);
    
    Task<AutoTestDataSet> GetAutoTestDataSetByIdAsync(int dataSetId, CancellationToken cancellationToken = default);

    Task DeleteAutoTestDataSetAsync(AutoTestDataSet dataSet, CancellationToken cancellationToken);
    
    Task<List<int>> GetDataItemIdsByDataSetIdAsync(int dataSetId, CancellationToken cancellationToken);

    Task DeleteAutoTestDataItemAsync(List<int> delIds, CancellationToken cancellationToken);
}

public partial class AutoTestDataProvider : IAutoTestDataProvider
{
    private readonly IMapper _mapper;
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAgentDataProvider _agentDataProvider;
    
    public AutoTestDataProvider(IRepository repository, IMapper mapper, IUnitOfWork unitOfWork, IAgentDataProvider agentDataProvider)
    {
        _mapper = mapper;
        _unitOfWork = unitOfWork;
        _agentDataProvider = agentDataProvider;
        _repository = repository;
    }

    public async Task AddAutoTestDataItemsAsync(List<AutoTestDataSetItem> setItems, CancellationToken cancellationToken)
    {
        await _repository.InsertAllAsync(setItems, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task AddAutoTestDataSetAsync(AutoTestDataSet dataSet, CancellationToken cancellationToken)
    {
        await _repository.InsertAsync(dataSet, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

        List<AutoTestDataItem> resultItems;

        if (page.HasValue && pageSize.HasValue)
        {
            resultItems = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page.Value - 1) * pageSize.Value)
                .Take(pageSize.Value)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            resultItems = await query
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
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
    
    
    public async Task DeleteAutoTestDataItemAsync(List<int> delIds, CancellationToken cancellationToken)
    {
        var deleteItems = await _repository.Query<AutoTestDataItem>()
            .Where(x => delIds.Contains(x.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        await _repository.DeleteAllAsync(deleteItems, cancellationToken).ConfigureAwait(false);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}