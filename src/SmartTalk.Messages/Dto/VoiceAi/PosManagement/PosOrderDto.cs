using SmartTalk.Messages.Enums.VoiceAi;

namespace SmartTalk.Messages.Dto.VoiceAi.PosManagement;

public class PosOrderDto
{
    public int Id { get; set; }
    
    public int StoreId { get; set; }
    
    public string Name { get; set; }
    
    public string Phone { get; set; }
    
    public string Address { get; set; }
    
    public string Latitude { get; set; }
    
    public string Longitude { get; set; }
    
    public string Room { get; set; }
    
    public string OrderNum { get; set; }
    
    public int Status { get; set; } = 10;
    
    public int Count { get; set; }
    
    public decimal Tax { get; set; }
    
    public decimal SubTotal { get; set; }
    
    public decimal Total { get; set; }
    
    public int Type { get; set; }
    
    public string Note { get; set; }
    
    public List<PosOrderItemDto> Items { get; set; }
}

public class PosOrderItemDto
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public PosOrderItemType Type { get; set; }
}