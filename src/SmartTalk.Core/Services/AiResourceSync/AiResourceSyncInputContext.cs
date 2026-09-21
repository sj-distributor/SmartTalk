using SmartTalk.Core.Domain.Pos;
using SmartTalk.Messages.Dto.AiResourceSync;
using SmartTalk.Messages.Dto.Sales;

namespace SmartTalk.Core.Services.AiResourceSync;

public class AiResourceSyncInputContext
{
    public Company Company { get; set; }

    public List<CrmSalesAutoSyncCustomerDto> Customers { get; set; }

    public List<CrmSalesAutoSyncCustomerGroup> CustomerGroups { get; set; }

    public IReadOnlyDictionary<string, CrmSalesAutoSyncCustomerGroup> CustomerIdLookup { get; set; }

    public HashSet<string> ActiveCustomerIds { get; set; }
    
    public bool IsFullSync { get; set; }
    
    public bool IsInitialRelease { get; set; }
    
    public AiResourceSyncExecutionStatsDto Stats { get; set; }
}
