using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Asterisk;

[Table("asterisk_cdr")]
public class AsteriskCdr : IEntity, IHasCreatedFields
{
    [Column("id")]
    public int Id { get; set; }

    [Column("src")]
    public string Src { get; set; }

    [Column("last_app")]
    public string LastApp { get; set; }

    [Column("disposition")]
    public string Disposition { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}