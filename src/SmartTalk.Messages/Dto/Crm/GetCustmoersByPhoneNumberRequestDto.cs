using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Dto.Crm;

using Newtonsoft.Json;

public class GetCustmoersByPhoneNumberRequestDto
{
    [JsonProperty("phone_number")]
    public string PhoneNumber { get; set; }
}

public class GetCustmoersByPhoneNumberResponseDto : SmartTalkResponse
{
    [JsonProperty("data")]
    public List<GetCustomersPhoneNumberDataDto> Data { get; set; }
}

public class GetCustomersPhoneNumberDataDto
{
    [JsonProperty("sap_id")]
    public string SapId { get; set; }
    
    [JsonProperty("customer_name")]
    public string CustomerName { get; set; }
    
    [JsonProperty("street")]
    public string Street { get; set; }
    
    [JsonProperty("warehouse")]
    public string Warehouse { get; set; }
    
    [JsonProperty("header_note_1")]
    public string HeaderNote1 { get; set; }
    
    [JsonProperty("contacts")]
    public List<ContactDto> Contacts { get; set; }
}

public class ContactDto
{
    [JsonProperty("name")]
    public string Name { get; set; }
    
    [JsonProperty("phone")]
    public string Phone { get; set; }
    
    [JsonProperty("identity")]
    public string Identity { get; set; }
    
    [JsonProperty("language")]
    public string Language { get; set; }
}