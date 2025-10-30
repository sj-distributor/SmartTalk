using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Messages.Commands.AutoTest;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestDataProvider : IScopedDependency
{
    Task<(int count, List<AutoTestDataSet>)> GetAutoTestDataSetsAsync(int? page, int? pageSize, string? keyName,
        CancellationToken cancellationToken);

    Task<(int count, List<AutoTestDataItem>)> GetAutoTestDataItemsByIdAsync(int dataSetId, int? page, int? pageSize,
        CancellationToken cancellationToken);

    Task CopyAutoTestDataItemsAsync(int sourceDataSetId, int targetDataSetId, CancellationToken cancellationToken);

    Task DeleteAutoTestDataSetAsync(int dataSetId, CancellationToken cancellationToken);

    Task AddAutoTestDataSetByQuoteAsync(int dataSetId, CancellationToken cancellationToken);
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

    public async Task<(int count, List<AutoTestDataSet>)> GetAutoTestDataSetsAsync(int? page, int? pageSize,
        string? keyName, CancellationToken cancellationToken)
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

        List<AutoTestDataItem> resultItems;

        var dataSets = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page.Value - 1) * pageSize.Value)
            .Take(pageSize.Value)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return (count, dataSets);
    }

    public async Task<(int count, List<AutoTestDataItem>)> GetAutoTestDataItemsByIdAsync(int dataSetId, int? page,
        int? pageSize, CancellationToken cancellationToken)
    {
        var query =
            from autoTestDataSetItem in _repository.Query<AutoTestDataSetItem>()
            join autoTestDataItem in _repository.Query<AutoTestDataItem>()
                on autoTestDataSetItem.DataItemId equals autoTestDataItem.Id
            join importRecord in _repository.Query<AutoTestImportDataRecord>()
                on autoTestDataItem.ImportRecordId equals importRecord.Id
            where autoTestDataSetItem.DataSetId == dataSetId
            select new { autoTestDataItem, importRecord };

        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        List<AutoTestDataItem> resultItems;

        if (page.HasValue && pageSize.HasValue)
        {
            var list = await query
                .OrderByDescending(x => x.autoTestDataItem.CreatedAt)
                .Skip((page.Value - 1) * pageSize.Value)
                .Take(pageSize.Value)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            resultItems = list.Select(x =>
            {
                x.autoTestDataItem.InputJson = JsonConvert.SerializeObject(x.importRecord);
                return x.autoTestDataItem;
            }).ToList();
        }
        else
        {
            var items = await query
                .OrderByDescending(x => x.autoTestDataItem.CreatedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            resultItems = items.Select(x =>
            {
                x.autoTestDataItem.InputJson = JsonConvert.SerializeObject(x.importRecord);
                return x.autoTestDataItem;
            }).ToList();
        }

        return (count, resultItems);
    }

    public async Task CopyAutoTestDataItemsAsync(int sourceDataSetId, int targetDataSetId,
        CancellationToken cancellationToken)
    {
        var dataItemIds = await _repository.Query<AutoTestDataSetItem>()
            .Where(x => x.DataSetId == sourceDataSetId)
            .Select(x => x.DataItemId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (dataItemIds.Count == 0) return;

        var now = DateTimeOffset.UtcNow;

        var newTargetItems = dataItemIds.Select(dataItemId => new AutoTestDataSetItem
        {
            DataSetId = targetDataSetId,
            DataItemId = dataItemId,
            CreatedAt = now
        }).ToList();

        await _repository.InsertAllAsync(newTargetItems, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAutoTestDataSetAsync(int dataSetId, CancellationToken cancellationToken)
    {
        var dataSet = await _repository.Query<AutoTestDataSet>()
            .FirstOrDefaultAsync(x => x.Id == dataSetId, cancellationToken)
            .ConfigureAwait(false);

        if (dataSet == null) return;

        if (dataSet.IsDelete) return;

        dataSet.IsDelete = true;

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAutoTestDataSetByQuoteAsync(int dataSetId, CancellationToken cancellationToken)
    {
        var dataSet = await _repository.Query<AutoTestDataSet>()
            .Where(x => x.Id == dataSetId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (dataSet == null) return;

        var newDataSet = new AutoTestDataSet
        {
            ScenarioId = dataSet.ScenarioId,
            KeyName = dataSet.KeyName + "-" + DateTimeOffset.UtcNow.ToString("yyyy:MM:dd:HH:mm:ss"),
            Name = dataSet.Name + "-" + DateTimeOffset.UtcNow.ToString("yyyy:MM:dd:HH:mm:ss"),
            IsDelete = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _repository.InsertAsync(newDataSet, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dataItemIds = await _repository.Query<AutoTestDataSetItem>()
            .Where(x => x.DataSetId == dataSetId)
            .Select(x => x.DataItemId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (dataItemIds.Count == 0) return;

        var newDataItems = dataItemIds.Select(dataItemId => new AutoTestDataSetItem
        {
            DataSetId = newDataSet.Id,
            DataItemId = dataItemId,
            CreatedAt = DateTimeOffset.UtcNow
        }).ToList();

        await _repository.InsertAllAsync(newDataItems, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}