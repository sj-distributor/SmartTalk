using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Restaurant;

[Table("restaurant_menu_item")]
public class RestaurantMenuItem : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("restaurant_id")]
    public int RestaurantId { get; set; }
    
    [Column("price")]
    public decimal Price { get; set; }
    
    [Column("name_en"), StringLength(256)]
    public string NameEn { get; set; }
    
    [Column("name_zh"), StringLength(256)]
    public string NameZh { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
}