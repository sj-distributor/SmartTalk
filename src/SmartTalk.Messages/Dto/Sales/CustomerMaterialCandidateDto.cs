namespace SmartTalk.Messages.Dto.Sales;

public class CustomerMaterialCandidateDto
{
    public string CustomerNumber { get; set; }

    public string SourceType { get; set; }

    public string MaterialNumber { get; set; }

    public string MaterialDescription { get; set; }

    public string Plant { get; set; }

    public string MaterialType { get; set; }

    public decimal Atr { get; set; }

    public string IsAssign { get; set; }

    public DateTime? LastInvoiceDate { get; set; }

    public DateTime? LastUpdate { get; set; }
}
