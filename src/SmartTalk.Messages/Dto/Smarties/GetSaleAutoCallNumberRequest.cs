namespace SmartTalk.Messages.Dto.Smarties;

public class GetSaleAutoCallNumberRequest
{
    public int Id { get; set; }
}

public class GetSaleAutoCallNumberResponse
{
    public GetSaleAutoCallNumberResponseData Data { get; set; }
}

public class GetSaleAutoCallNumberResponseData
{
    public SettingNumberDto Number { get; set; }
}

public class SettingNumberDto
{
    public int Id { get; set; }
    
    public Guid SaleAutoCallSettingId { get; set; }
    
    public string TargetPhoneNumber { get; set; }
    
    public string Greeting { get; set; }
}