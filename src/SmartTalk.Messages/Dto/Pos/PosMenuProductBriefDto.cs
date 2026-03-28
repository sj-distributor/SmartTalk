namespace SmartTalk.Messages.Dto.Pos;

public class PosMenuProductBriefDto
{
    public string ProductId { get; set; }
    
    public string Name { get; set; }
    
    public string CategoryName { get; set; }
    
    public decimal Price { get; set; }
    
    public string Tax { get; set; }
    
    public string Modifiers { get; set; }
}

