using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;
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
        // set
        
        // job
        await _autoTestDataImportHandlerSwitcher.GetHandler(command.ImportType).ImportAsync(command.ImportData, cancellationToken).ConfigureAwait(false);
        
        // set
        return new AutoTestImportDataResponse();
    }
}