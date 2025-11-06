using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Messages.Dto.AutoTest;

public class AutoTestOrderItemDto
{
    public string Material { get; set; }
    
    public decimal Quantity { get; set; }
    
    public string MaterialName { get; set; }
    
    public AutoTestOrderItemStatus ItemStatus { get; set; }
}