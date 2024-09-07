using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.Restaurants;

namespace SmartTalk.Core.Domain.Restaurants;

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
    
    [Column("name"), StringLength(256)]
    public string Name { get; set; }

    [Column("language")] 
    public RestaurantItemLanguage Language { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
}