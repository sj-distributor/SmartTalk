namespace SmartTalk.Messages.Dto.Smarties;

public class GetNumberGreetingRequest
{
    public int Id { get; set; }
}

public class GetNumberGreetingResponse
{
    public GetNumberGreetingResponseData Data { get; set; }
}

public class GetNumberGreetingResponseData
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