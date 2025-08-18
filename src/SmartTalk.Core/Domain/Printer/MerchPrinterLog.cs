using SmartTalk.Messages.Enums.Printer;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Printer;

[Table("merch_printer_log")]
public class MerchPrinterLog : IEntity
{
    public MerchPrinterLog()
    {
        CreatedDate=DateTimeOffset.Now;
    }
    
    [Key]
    [Column("id", TypeName = "char(36)")]
    public Guid Id { get; set; }

    [Column("agent_id")]
    public int AgentId { get; set; }
        
    [Column("order_id", TypeName = "char(36)")]
    public Guid? OrderId { get; set; }

    [Column("printer_mac")]
    public string PrinterMac { get; set; }

    [Column("message")]
    public string Message { get; set; }

    [Column("print_log_type")]
    public PrintLogType PrintLogType { get; set; }
        
    [Column("code")]
    public int? Code { get; set; }

    [Column("code_description")]
    public string CodeDescription { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}
