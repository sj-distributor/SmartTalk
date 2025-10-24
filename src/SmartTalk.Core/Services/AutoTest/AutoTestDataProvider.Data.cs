using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;
public partial interface IAutoTestDataProvider
{
    Task<List<AutoTestDataItem>> GetAutoTestDataItemsBySetIdAsync(int dataSetId, CancellationToken cancellationToken);
}

public partial class AutoTestDataProvider
{
    public async Task<List<AutoTestDataItem>> GetAutoTestDataItemsBySetIdAsync(int dataSetId, CancellationToken cancellationToken)
    {
        return await (from testDataSetItem in _repository.Query<AutoTestDataSetItem>().Where(x => x.DataSetId == dataSetId)
            join dataItem in _repository.Query<AutoTestDataItem>() on testDataSetItem.DataItemId equals dataItem.Id 
            select dataItem).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}