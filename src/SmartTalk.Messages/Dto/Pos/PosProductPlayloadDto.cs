namespace SmartTalk.Messages.Dto.Pos;

public class PosProductPlayloadDto
{
    public int Id { get; set; }

    public int CategoryId { get; set; }
    
    public decimal Price { get; set; }
    
    public string Names { get; set; }
    
    public string ProductId { get; set; }
}