namespace SmartTalk.Messages.Dto.Sales;

public class GetCustomerLevel5HabitResponseDto
{
    public List<HistoryCustomerLevel5HabitDto> HistoryCustomerLevel5HabitDtos { get; set; } 
}

public class HistoryCustomerLevel5HabitDto
{ 
    public string CustomerId { get; set; }

    public string LevelCode5 { get; set; }

    public List<CustomerLikeNameDto> CustomerLikeNames { get; set; } = new();

    public List<MaterialPartInfoDto> MaterialPartInfoDtos { get; set; }
}

public class MaterialPartInfoDto
{
    public string MaterialNumber { get; set; }

    public string BaseUnit { get; set; }

    public string SalesUnit { get; set; }

    public decimal Weights { get; set; }

    public string PlaceOfOrigin { get; set; }

    public string Packing { get; set; }

    public string Specifications { get; set; }

    public string Ranks { get; set; }

    public int Atr { get; set; }
}

public class CustomerLikeNameDto
{
    public DateTime CreateDate { get; set; }

    public string CustomerLikeName { get; set; }
}