using System.Text.Json;
using Newtonsoft.Json;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestService
{
    Task<AutoTestImportDataResponse> AutoTestImportDataAsync(AutoTestImportDataCommand command, CancellationToken cancellationToken);
}

public partial class AutoTestService
{
    public async Task<AutoTestImportDataResponse> AutoTestImportDataAsync(AutoTestImportDataCommand command, CancellationToken cancellationToken)
    {
        var dataSet = new AutoTestDataSet()
        {
            ScenarioId = command.ScenarioId,
            KeyName = command.KeyName,
            Name = command.KeyName,
            IsDelete = false
        };

        await _autoTestDataProvider.AddAutoTestDataSetAsync(dataSet, cancellationToken:cancellationToken).ConfigureAwait(false);
        
        var scenario = await _autoTestDataProvider.GetAutoTestScenarioByIdAsync(command.ScenarioId, cancellationToken).ConfigureAwait(false);
        
        var importRecord = new AutoTestImportDataRecord 
        { 
            ScenarioId = scenario.Id, 
            Type = AutoTestImportDataRecordType.Api, 
            Status = AutoTestStatus.Running, 
            OpConfig = JsonConvert.SerializeObject(command.ImportData), 
            CreatedAt = DateTimeOffset.Now 
        }; 
        
        await _autoTestDataProvider.AddAutoTestImportRecordAsync(importRecord, true, cancellationToken).ConfigureAwait(false);
        
        await _autoTestDataImportHandlerSwitcher.GetHandler(command.ImportType).ImportAsync(command.ImportData, command.ScenarioId, dataSet.Id, cancellationToken).ConfigureAwait(false);

        return new AutoTestImportDataResponse()
        {
            Data = _mapper.Map<AutoTestDataSetDto>(dataSet),
        };
    }
}