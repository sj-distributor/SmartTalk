using System.Text.Json;
using Newtonsoft.Json;
using Serilog;
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
        if (!command.ImportData.TryGetValue("CustomerId", out var customerObj)) 
            throw new Exception($"The imported data is missing customerId, so processing is skipped. command: {command}");
        
        var customerIdRaw = command.ImportData["CustomerId"]?.ToString() ?? "";
        var customerIdFormatted = customerIdRaw.PadLeft(5, '0'); 
        var startDate = (DateTime)command.ImportData["StartDate"]; 
        var endDate = (DateTime)command.ImportData["EndDate"]; 
        
        var keyName = $"{customerIdFormatted}-{startDate}-{endDate}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        
        var dataSet = new AutoTestDataSet()
        {
            ScenarioId = command.ScenarioId,
            KeyName = keyName,
            Name = keyName,
            IsDelete = false
        };

        await _autoTestDataProvider.AddAutoTestDataSetAsync(dataSet, cancellationToken:cancellationToken).ConfigureAwait(false);
        
        var scenario = await _autoTestDataProvider.GetAutoTestScenarioByIdAsync(command.ScenarioId, cancellationToken).ConfigureAwait(false);
        
        var importRecord = new AutoTestImportDataRecord 
        { 
            ScenarioId = command.ScenarioId, 
            Type = AutoTestImportDataRecordType.Api, 
            Status = AutoTestStatus.Running, 
            OpConfig = JsonConvert.SerializeObject(command.ImportData), 
            CreatedAt = DateTimeOffset.Now 
        }; 
        
        if (scenario == null)
        {
            Log.Information("Scenario {ScenarioId} does not exist, skip importing.", command.ScenarioId);
            
            importRecord.Status = AutoTestStatus.Failed;
            
            await _autoTestDataProvider.AddAutoTestImportRecordAsync(importRecord, true, cancellationToken).ConfigureAwait(false);
            
            dataSet.ImportRecordId = importRecord.Id;
        
            await _autoTestDataProvider.UpdateAutoTestDataSetAsync(dataSet, true, cancellationToken).ConfigureAwait(false);
            
            return new AutoTestImportDataResponse()
            {
                Data = _mapper.Map<AutoTestDataSetDto>(dataSet),
            };
        }
        
        await _autoTestDataProvider.AddAutoTestImportRecordAsync(importRecord, true, cancellationToken).ConfigureAwait(false);

        dataSet.ImportRecordId = importRecord.Id;
        
        await _autoTestDataProvider.UpdateAutoTestDataSetAsync(dataSet, true, cancellationToken).ConfigureAwait(false);

        _smartTalkBackgroundJobClient.Enqueue(() => _autoTestDataImportHandlerSwitcher.GetHandler(command.ImportType).ImportAsync(command.ImportData, command.ScenarioId, dataSet.Id, importRecord.Id, cancellationToken));

        return new AutoTestImportDataResponse()
        {
            Data = _mapper.Map<AutoTestDataSetDto>(dataSet),
        };
    }
}