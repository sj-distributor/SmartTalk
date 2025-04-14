namespace SmartTalk.Messages.Dto.Restaurant;

public class ModifierProductGroupDto
{
    public string LanguageCode { get; set; }
    
    public string GroupName { get; set; }
    
    public decimal ItemPrice { get; set; }
    
    public int MinimumSelect { get; set; }
    
    public int MaximumSelect { get; set; }
    
    public int MaximumRepetition { get; set; }
    
    public List<ModifierPromptItemDto> ModifierItems { get; set; } = new();
}

public class ModifierPromptItemDto
{
    public string Name { get; set; }
    
    public decimal Price { get; set; }
    
    public decimal? OriginalPrice { get; set; }
    
    public string Size { get; set; }
}
