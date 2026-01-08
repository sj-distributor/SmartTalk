using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Messages.Dto.AutoTest;

public class AutoTestOrderItemDto
{
    public string ItemId { get; set; }
    
    public decimal Quantity { get; set; }
    
    public string Unit { get; set; }
    
    public string ItemName { get; set; }
    
    public AutoTestOrderItemStatus Status { get; set; }
}