using Newtonsoft.Json;
using SmartTalk.Messages.Requests.Printer;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Printer;

[Table("merch_printer")]
public class MerchPrinter : IEntity
{
    public MerchPrinter()
    {
        StatusInfoLastModifiedDate = DateTimeOffset.Now;
    }
    
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("agent_id")]
    public int AgentId { get; set; }

    [Column("printer_name")]
    public string PrinterName { get; set; }

    [Column("printer_mac")]
    public string PrinterMac { get; set; }

    [Column("status_info")]
    public string StatusInfo { get; set; }

    [Column("is_enabled", TypeName = "tinyint(1) unsigned")]
    public bool IsEnabled { get; set; }

    [Column("token", TypeName = "char(36)")]
    public Guid Token { get; set; }

    [Column("status_info_last_modified_date")]
    public DateTimeOffset StatusInfoLastModifiedDate { get; set; }

    public PrinterStatusInfo PrinterStatusInfo()
    {
        if (string.IsNullOrWhiteSpace(StatusInfo) || StatusInfoLastModifiedDate < DateTimeOffset.Now.AddMinutes(-2)) return null;

        return JsonConvert.DeserializeObject<PrinterStatusInfo>(StatusInfo);
    }
        
}