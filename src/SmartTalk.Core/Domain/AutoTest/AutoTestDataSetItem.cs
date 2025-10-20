using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.AutoTest;

[Table("auto_test_data_set_item")]
public class AutoTestDataSetItem : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("data_set_id")]
    public int DataSetId { get; set; }
    
    [Column("data_item_id")]
    public int DataItemId { get; set; }
    
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}