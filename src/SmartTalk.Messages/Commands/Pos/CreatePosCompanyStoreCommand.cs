using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class CreatePosCompanyStoreCommand : ICommand
{
    public int CompanyId { get; set; }
    
    public string Names { get; set; }
    
    public string Address { get; set; }
    
    public string Latitude { get; set; }
    
    public string Longitude { get; set; }
    
    public List<string> PhoneNumbers { get; set; }
}

public class CreatePosCompanyStoreResponse : SmartTalkResponse<PosCompanyStoreDto>;