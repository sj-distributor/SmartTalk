using SmartTalk.Core.Domain.Pos;
using SmartTalk.Messages.Dto.AiResourceSync;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Core.Services.AiResourceSync;

public class AiResourceSyncInputContext
{
    public Company Company { get; set; }
    
    // - Customers: 原始客户列表
    public List<CrmSalesAutoSyncCustomerDto> Customers { get; set; }
    
    // - CustomerGroups: 按销售员/语言/客户关系归并后的分组
    public List<CrmSalesAutoSyncCustomerGroup> CustomerGroups { get; set; }
    
    // - CustomerIdLookup: 用 customerId 反查它属于哪个分组
    public IReadOnlyDictionary<string, CrmSalesAutoSyncCustomerGroup> CustomerIdLookup { get; set; }
    
    // - ActiveCustomerIds: 当前还“活着”的客户集合，供全量收尾时判断哪些助理该停用
    public HashSet<string> ActiveCustomerIds { get; set; }
    
    public bool IsFullSync { get; set; }
    
    public bool IsInitialRelease { get; set; }
    
    public AiResourceSyncExecutionStatsDto Stats { get; set; }
}
