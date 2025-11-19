using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Sales;

[Table("customer_items_cache")]
public class CustomerItemsCache : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("cache_key")]
    public string CacheKey { get; set; }

    [Column("cache_value")]
    public string CacheValue { get; set; }

    [Column("last_updated")]
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.Now;
}