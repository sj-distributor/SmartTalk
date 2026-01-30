using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class UpdateCompanyStoreCommand : ICommand
{
    public int Id { get; set; }
    
    public string Logo { get; set; }
    
    public string Names { get; set; }
    
    public string Address { get; set; }
    
    public string Latitude { get; set; }
    
    public string Longitude { get; set; }
    
    public string Description { get; set; }

    public string Timezone { get; set; }
    
    public bool IsManualReview { get; set; }
    
    public List<string> PhoneNumbers { get; set; }
}

public class UpdateCompanyStoreResponse : SmartTalkResponse<CompanyStoreDto>;