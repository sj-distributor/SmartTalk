namespace SmartTalk.Messages.Dto.WebSocket;

public class Food
{
    public string Id { get; set; }
    public double Price { get; set; }
        
    public int Quantity { get; set; }
    public string EnglishName { get; set; }
    public string CineseName { get; set; }
        
    public string Notes { get; set; }
}