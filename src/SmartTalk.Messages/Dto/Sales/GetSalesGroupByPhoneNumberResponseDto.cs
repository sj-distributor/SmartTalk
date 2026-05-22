namespace SmartTalk.Messages.Dto.Sales;

public class GetSalesGroupByPhoneNumberResponseDto
{
    public SalesGroupTableDto Table { get; set; }

    public List<SalesGroupRowDto> Rows { get; set; } = [];

    public int RowCount { get; set; }

    public int TotalRows { get; set; }
}

public class SalesGroupTableDto
{
    public string Id { get; set; }

    public string Name { get; set; }
}

public class SalesGroupRowDto
{
    public string PhoneNumber { get; set; }

    public string Sales { get; set; }

    public string SalesGroup { get; set; }
}
