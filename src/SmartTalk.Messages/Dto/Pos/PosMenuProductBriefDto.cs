namespace SmartTalk.Messages.Dto.Pos;

public class PosMenuProductBriefDto
{
    public string ProductId { get; set; }

    public string Name { get; set; }

    public string NameCn { get; set; }

    public string NameEn { get; set; }

    public string CategoryName { get; set; }

    public decimal Price { get; set; }

    public string Tax { get; set; }

    public string Specification { get; set; }

    public List<PosMenuProductModifierGroupDto> ModifierGroups { get; set; } = [];

    public List<PosMenuBriefDto> PosMenus { get; set; } = [];
}

public class PosMenuProductModifierGroupDto
{
    public long Id { get; set; }

    public string Name { get; set; }

    public int MinimumSelect { get; set; }

    public int MaximumSelect { get; set; }

    public int MaximumRepetition { get; set; }

    public List<PosMenuProductModifierOptionDto> Options { get; set; } = [];
}

public class PosMenuProductModifierOptionDto
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public string Name { get; set; }

    public decimal Price { get; set; }
}

public class PosMenuBriefDto
{
    public string Name { get; set; }

    public string TimePeriod { get; set; }
}
