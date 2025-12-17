namespace SmartTalk.Messages.Dto.Pos;

public class PosProductSimpleModifiersDto
{
    public string ProductId { get; set; }
    
    public string ModifierId { get; set; }
    
    public int MinimumSelect { get; set; }
    
    public int MaximumSelect { get; set; }
    
    public int MaximumRepetition { get; set; }

    public List<string> ModifierProductIds { get; set; } = [];
}