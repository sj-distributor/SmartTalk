using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Dto.Crm;

public class GetCustmoersByPhoneNumberRequestDto
{
    public string PhoneNumber { get; set; }
}

public class GetCustmoersByPhoneNumberResponseDto : SmartTalkResponse
{
    public List<GetCustomersPhoneNumberDataDto> Data { get; set; }
}

public class GetCustomersPhoneNumberDataDto
{
    public string SapId { get; set; }
    
    public string CustomerName { get; set; }
    
    public string Street { get; set; }
    
    public string Warehouse { get; set; }
    
    public string HeaderNote1 { get; set; }
    
    public List<ContactDto> Contacts { get; set; }
}

public class ContactDto
{
    public string Name { get; set; }
    
    public string Phone { get; set; }
    
    public string Identity { get; set; }
    
    public string Language { get; set; }
}