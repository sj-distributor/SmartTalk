namespace SmartTalk.Messages.Dto.Sales;

public class CrmSalesAutoSyncQueryDto
{
    public DateTimeOffset? StartAt { get; set; }

    public DateTimeOffset? EndAt { get; set; }

    public bool IsInitialSync { get; set; }

    public bool IsManual { get; set; }
}

public class CrmSalesAutoSyncCustomerDto
{
    public string CustomerId { get; set; }

    public string CustomerName { get; set; }

    public string SalesName { get; set; }

    public string SalesGroup { get; set; }

    public string Language { get; set; }

    public bool IsApproved { get; set; }

    public DateTimeOffset? ChangedAt { get; set; }

    public List<int> SourceSceneIds { get; set; } = new();
}
