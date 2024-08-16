using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Attachments;

[Table("attachment")]
public class Attachment : IEntity
{
    public Attachment()
    {
        CreatedDate = DateTimeOffset.Now;
    }
    
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
    
    [Column("uuid", TypeName = "char(36)")]
    public Guid Uuid { get; set; }
    
    [Column("file_url")]
    [StringLength(512)]
    public string FileUrl { get; set; }
    
    [Column("file_name")]
    public string FileName { get; set; }
    
    [Column("file_size")]
    public long FileSize { get; set; }
    
    [Column("file_path"), StringLength(1024)]
    public string FilePath { get; set; }
    
    [Column("origin_file_name"), StringLength(1024)]
    public string OriginFileName { get; set; }
}