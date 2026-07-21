using SmartTalk.Core.Domain.Pos;
using SmartTalk.Messages.Dto.AiResourceSync;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Core.Services.AiResourceSync;

public class AiResourceSyncShardExecutionResult
{
    // 当前分片对应的销售 key / 门店名。
    public string SalesKey { get; set; }

    // 这个分片内一共处理了多少个客户组。
    public int CustomerGroupCount { get; set; }

    // 当前分片自己的执行统计，例如创建了多少助理、迁移了多少助理等。
    public AiResourceSyncExecutionStatsDto Stats { get; set; }
}

public class AiResourceSyncExecutionResult
{
    // 本次同步总共处理了多少客户。
    public int TotalCount { get; set; }

    // 本次同步最终拆成了多少个门店分片。
    public int ShardCount { get; set; }

    // 每个分片各自的执行结果。
    public IReadOnlyList<AiResourceSyncShardExecutionResult> Shards { get; set; }

    // 整个同步的汇总统计。
    public AiResourceSyncExecutionStatsDto Stats { get; set; }

    // 是否属于首次发布同步。
    public bool IsInitialRelease { get; set; }
}

internal class AiResourceSyncCustomerLoadResult
{
    // 当前同步对应的销售公司。
    public Company Company { get; set; }

    // 本次从 CRM 拉回来的客户列表。
    public List<CrmSalesAutoSyncCustomerDto> Customers { get; set; }

    // CRM 返回的总数量；有些场景下如果 CRM 没单独给，就退化为 Customers.Count。
    public int TotalCount { get; set; }

    // 是否为全量同步。
    public bool IsFullSync { get; set; }

    // 是否为首次发布同步。
    public bool IsInitialRelease { get; set; }
}
