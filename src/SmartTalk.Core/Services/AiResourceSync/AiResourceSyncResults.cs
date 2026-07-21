using SmartTalk.Core.Domain.Pos;
using SmartTalk.Messages.Dto.AiResourceSync;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Core.Services.AiResourceSync;

public class AiResourceSyncShardExecutionResult
{
    public string SalesKey { get; set; }

    public int CustomerGroupCount { get; set; }

    public AiResourceSyncExecutionStatsDto Stats { get; set; }
}

public class AiResourceSyncExecutionResult
{
    public int TotalCount { get; set; }

    public int ShardCount { get; set; }

    public IReadOnlyList<AiResourceSyncShardExecutionResult> Shards { get; set; }

    public AiResourceSyncExecutionStatsDto Stats { get; set; }

    public bool IsInitialRelease { get; set; }
}

internal class AiResourceSyncCustomerLoadResult
{
    public Company Company { get; set; }

    public List<CrmSalesAutoSyncCustomerDto> Customers { get; set; }

    public int TotalCount { get; set; }

    public bool IsFullSync { get; set; }

    public bool IsInitialRelease { get; set; }
}
