using SmartTalk.Messages.Enums.Printer;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Printer;

[Table("merch_printer_order")]
public class MerchPrinterOrder : IEntity
{
    public MerchPrinterOrder()
    {
        CreatedDate = DateTimeOffset.Now;
        PrintStatus = PrintStatus.Waiting;
    }
    
    [Key]
    [Column("id", TypeName = "char(36)")]
    public Guid Id { get; set; }

    [Column("store_id")]
    public int StoreId { get; set; }
        
    [Column("order_id")]
    public int? OrderId { get; set; }

    [Column("phone_order_Id")]
    public int? PhoneOrderId { get; set; }
        
    [Column("print_status")]
    public PrintStatus PrintStatus { get; set; }

    [Column("print_date")]
    public DateTimeOffset PrintDate { get; set; }

    [Column("print_error_times")]
    public int PrintErrorTimes { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }

    [Column("image_url")]
    public string ImageUrl { get; set; }

    [Column("image_key")]
    public string ImageKey { get; set; }

    [Column("printer_mac")]
    public string PrinterMac { get; set; }

    [Column("print_format")]
    public PrintFormat PrintFormat { get; set; }
}