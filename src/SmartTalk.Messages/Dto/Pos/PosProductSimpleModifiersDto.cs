namespace SmartTalk.Messages.Dto.Pos;

public class PosProductSimpleModifiersDto
{
    public string ProductId { get; set; }
    
    public int MinimumSelect { get; set; }
    
    public int MaximumSelect { get; set; }
    
    public int MaximumRepetition { get; set; }
}