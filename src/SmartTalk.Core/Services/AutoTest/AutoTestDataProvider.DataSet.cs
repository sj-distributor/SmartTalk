using SmartTalk.Core.Domain.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestDataProvider
{
    Task AddAutoTestDataSetAsync(AutoTestDataSet dataSet, bool forceSave = true, CancellationToken cancellationToken = default);
}

public partial class AutoTestDataProvider
{
    public async Task AddAutoTestDataSetAsync(AutoTestDataSet dataSet, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(dataSet, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}