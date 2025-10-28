using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestDataProvider : IScopedDependency
{
    Task<(int count, List<AutoTestDataSet>)> GetAutoTestDataSetsAsync(int? page, int? pageSize, CancellationToken cancellationToken);

    Task<(int count, List<AutoTestDataItem>)> GetAutoTestDataItemsByIdAsync(int dataSetId, int? page, int? pageSize, CancellationToken cancellationToken);

    Task CopyAutoTestDataItemsAsync(int sourceDataSetId, int targetDataSetId, CancellationToken cancellationToken);

    Task DeleteAutoTestDataSetAsync(int dataSetId, CancellationToken cancellationToken);
}

public partial class AutoTestDataProvider : IAutoTestDataProvider
{
    private readonly IMapper _mapper;
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    
    public AutoTestDataProvider(IRepository repository, IMapper mapper, IUnitOfWork unitOfWork)
    {
        _mapper = mapper;
        _unitOfWork = unitOfWork;
        _repository = repository;
    }
    
    public async Task<(int count, List<AutoTestDataSet>)> GetAutoTestDataSetsAsync(int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var query = _repository.Query<AutoTestDataSet>().Where(x => x.IsDelete == false);
        
        var  count = await query.CountAsync(cancellationToken).ConfigureAwait(false);

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

    public async Task<(int count, List<AutoTestDataItem>)> GetAutoTestDataItemsByIdAsync(int dataSetId, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var query =
            from autoTestDataSetItem in _repository.Query<AutoTestDataSetItem>().AsNoTracking()
            join autoTestDataItem in _repository.Query<AutoTestDataItem>().AsNoTracking()
                on autoTestDataSetItem.DataItemId equals autoTestDataItem.Id
            join importRecord in _repository.Query<AutoTestImportDataRecord>().AsNoTracking()
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

    public async Task CopyAutoTestDataItemsAsync(int sourceDataSetId, int targetDataSetId, CancellationToken cancellationToken)
    {
        var sourceItems = await _repository.Query<AutoTestDataSetItem>()
            .Where(x => x.DataSetId == sourceDataSetId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (sourceItems.Count == 0) return;
        
        var dataItemIds = sourceItems.Select(x => x.DataItemId).ToList();
        
        var existingTargetItems = await _repository.Query<AutoTestDataSetItem>()
            .Where(x => x.DataSetId == targetDataSetId && dataItemIds.Contains(x.DataItemId))
            .Select(x => x.DataItemId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        
        var newDataItemIds = dataItemIds.Except(existingTargetItems).ToList();

        if (newDataItemIds.Count == 0) return;
        
        var newTargetItems = newDataItemIds.Select(dataItemId => new AutoTestDataSetItem
        {
            DataSetId = targetDataSetId,
            DataItemId = dataItemId,
            CreatedAt = DateTimeOffset.Now
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
        
        if(dataSet.IsDelete)return;
        
        dataSet.IsDelete = true;
        
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}