namespace SmartTalk.Messages.Requests.Sales;

public class CrmCustomerApprovedCallbackRequest
{
    public string CustomerId { get; set; }

    public string CustomerName { get; set; }

    public string SalesName { get; set; }

    public string SalesGroup { get; set; }

    public string Language { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public List<int> SourceSceneIds { get; set; } = new();
}
