using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Printer;

[Table("printer_token")]
public class PrinterToken : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id", TypeName = "char(36)")]
    public Guid Id { get; set; }

    [Column("printer_mac")]
    public string PrinterMac { get; set; }

    [Column("token")]
    public Guid Token { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}