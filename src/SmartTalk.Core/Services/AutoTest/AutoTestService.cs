using AutoMapper;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.AutoTest;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Requests.AutoTest;

using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestService : IScopedDependency
{
    Task<AutoTestRunningResponse> AutoTestRunningAsync(AutoTestRunningCommand command, CancellationToken cancellationToken);
    
    Task<GetAutoTestDataSetResponse> GetAutoTestDataSetsAsync(GetAutoTestDataSetRequest request, CancellationToken cancellationToken);
    
    Task<GetAutoTestDataItemsByIdResponse> GetAutoTestDataItemsByIdAsync(GetAutoTestDataItemsByIdRequest request, CancellationToken cancellationToken);

    Task<CopyAutoTestDataSetResponse> CopyAutoTestDataItemsAsync(CopyAutoTestDataSetRequest request, CancellationToken cancellationToken);

    Task<DeleteAutoTestDataSetResponse> DeleteAutoTestDataSetAsync(DeleteAutoTestDataSetCommand command, CancellationToken cancellationToken);

    Task<AddAutoTestDataSetByQuoteResponse> AddAutoTestDataSetByQuoteAsync(AddAutoTestDataSetByQuoteCommand byQuoteCommand, CancellationToken cancellationToken);
}

public partial class AutoTestService : IAutoTestService
{
    private readonly IMapper _mapper;
    private readonly IAutoTestDataProvider _autoTestDataProvider;
    private readonly IAutoTestActionHandlerSwitcher _autoTestActionHandlerSwitcher;
    private readonly IAutoTestDataImportHandlerSwitcher _autoTestDataImportHandlerSwitcher;

    public AutoTestService(IMapper mapper, IAutoTestDataProvider autoTestDataProvider, IAutoTestActionHandlerSwitcher autoTestActionHandlerSwitcher, IAutoTestDataImportHandlerSwitcher autoTestDataImportHandlerSwitcher)
    {
        _mapper = mapper;
        _autoTestDataProvider = autoTestDataProvider;
        _autoTestActionHandlerSwitcher = autoTestActionHandlerSwitcher;
        _autoTestDataImportHandlerSwitcher = autoTestDataImportHandlerSwitcher;
    }
    
    public async Task<AutoTestRunningResponse> AutoTestRunningAsync(AutoTestRunningCommand command, CancellationToken cancellationToken)
    {
        var scenario = await _autoTestDataProvider.GetAutoTestScenarioByIdAsync(command.ScenarioId, cancellationToken).ConfigureAwait(false);
        
        var taskRecords = await _autoTestDataProvider.GetPendingTaskRecordsByTaskIdAsync(command.TaskId, cancellationToken).ConfigureAwait(false);
        
        taskRecords.ForEach(x => x.Status = AutoTestTaskRecordStatus.Ongoing);
        
        await _autoTestDataProvider.UpdateTaskRecordsAsync(taskRecords, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var executionResult = await _autoTestActionHandlerSwitcher.GetHandler(scenario.ActionType).ActionHandleAsync(scenario, command.TaskId, cancellationToken).ConfigureAwait(false);
        
        return new AutoTestRunningResponse() { Data = executionResult };
    }

    public async Task<GetAutoTestDataSetResponse> GetAutoTestDataSetsAsync(GetAutoTestDataSetRequest request, CancellationToken cancellationToken)
    {
        var (count, dataSets) = await _autoTestDataProvider.GetAutoTestDataSetsAsync(request?.Page, request?.PageSize, request?.KeyName, cancellationToken).ConfigureAwait(false);
        
        return new GetAutoTestDataSetResponse
        {
            Data = new GetAutoTestDataSetData()
            {
                Count = count,
                Records = dataSets.Select(x => _mapper.Map<AutoTestDataSetDto>(x)).ToList()
            }
        };
    }

    public async Task<GetAutoTestDataItemsByIdResponse> GetAutoTestDataItemsByIdAsync(GetAutoTestDataItemsByIdRequest request, CancellationToken cancellationToken)
    {
        var (count, dataItems) = await _autoTestDataProvider.GetAutoTestDataItemsByIdAsync(request.DataSetId, request.Page, request.PageSize, cancellationToken).ConfigureAwait(false);

        return new GetAutoTestDataItemsByIdResponse
        {
            Data = new GetAutoTestDataItemsByIdData
            {
                Count = count,
                Records = dataItems.Select(x => _mapper.Map<AutoTestDataItemDto>(x)).ToList()
            }
        };
    }

    public async Task<CopyAutoTestDataSetResponse> CopyAutoTestDataItemsAsync(CopyAutoTestDataSetRequest request, CancellationToken cancellationToken)
    { 
        var itemIds = await _autoTestDataProvider.GetDataItemIdsByDataSetIdAsync(request.SourceDataSetId, cancellationToken).ConfigureAwait(false);
        
        if (itemIds.Count == 0) return new CopyAutoTestDataSetResponse();
        
        var newTargetItems = itemIds.Select(dataItemId => new AutoTestDataSetItem
        {
            DataSetId = request.TargetDataSetId,
            CreatedAt = DateTimeOffset.UtcNow
        }).ToList();
        
        await _autoTestDataProvider.AddAutoTestDataItemsAsync(newTargetItems, cancellationToken).ConfigureAwait(false);
        
        return new CopyAutoTestDataSetResponse();
    }

    public async Task<DeleteAutoTestDataSetResponse> DeleteAutoTestDataSetAsync(DeleteAutoTestDataSetCommand command, CancellationToken cancellationToken)
    {
        var dataSet = await _autoTestDataProvider.GetAutoTestDataSetByIdAsync(command.AutoTestDataSetId, cancellationToken).ConfigureAwait(false);

        if (dataSet == null) throw new Exception("DataSet not found");
        
        await _autoTestDataProvider.DeleteAutoTestDataSetAsync(dataSet, cancellationToken).ConfigureAwait(false);
       
        return new DeleteAutoTestDataSetResponse();
    }

    public async Task<AddAutoTestDataSetByQuoteResponse> AddAutoTestDataSetByQuoteAsync(AddAutoTestDataSetByQuoteCommand command, CancellationToken cancellationToken)
    {
        var sets = await _autoTestDataProvider.GetAutoTestDataSetByIdAsync(command.DataSetId, cancellationToken).ConfigureAwait(false);
        
        if(sets == null) throw new Exception("DataSet not found");
        
        var newDataSet = new AutoTestDataSet
        {
            ScenarioId = sets.ScenarioId,
            KeyName = sets.KeyName + "-" + DateTimeOffset.UtcNow.ToString("yyyy:MM:dd:HH:mm:ss"),
            Name = sets.Name + "-" + DateTimeOffset.UtcNow.ToString("yyyy:MM:dd:HH:mm:ss"),
            IsDelete = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        
        await _autoTestDataProvider.AddAutoTestDataSetAsync(newDataSet, cancellationToken).ConfigureAwait(false);
        
        var dataItemIds = await _autoTestDataProvider.GetDataItemIdsByDataSetIdAsync(command.DataSetId, cancellationToken).ConfigureAwait(false);
        
        var newDataItems = dataItemIds.Select(dataItemId => new AutoTestDataSetItem
        {
            DataSetId = newDataSet.Id,
            DataItemId = dataItemId,
            CreatedAt = DateTimeOffset.UtcNow
        }).ToList();
        
        await _autoTestDataProvider.AddAutoTestDataSetByQuoteAsync(newDataItems, cancellationToken).ConfigureAwait(false);

        return new AddAutoTestDataSetByQuoteResponse();
    }
}