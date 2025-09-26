namespace SmartTalk.Messages.Dto.Sales;

public class GetCustomerLevel5HabitRequstDto
{
    public string CustomerId { get; set; }

    public List<string> LevelCode5List { get; set; }
}