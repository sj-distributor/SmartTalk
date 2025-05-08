namespace SmartTalk.Messages.Dto.VoiceAi.PosManagement;

public class PosProductModifierDto
{
    public int Id { get; set; }
    
    public string NameZh { get; set; }
    
    public string NameEn { get; set; }
    
    public string PosNameZh { get; set; }
    
    public string PosNameEn { get; set; }
    
    public int MinSelect { get; set; }
    
    public int MaxSelect { get; set; }
    
    public bool AllowDuplicate { get; set; }
    
    public List<ProductModifierOptionDto> Options { get; set; }
}

public class ProductModifierOptionDto
{
    public int Id { get; set; }
    
    public string NameZh { get; set; }
    
    public string NameEn { get; set; }
    
    public string PosNameZh { get; set; }
    
    public string PosNameEn { get; set; }
    
    public decimal Price { get; set; }
}