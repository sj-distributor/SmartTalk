using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;

namespace SmartTalk.Messages.Commands.VoiceAi.PosManagement;

public class UpdatePosCompanyStoreCommand : ICommand
{
    public int Id { get; set; }
    
    public string Logo { get; set; }
    
    public string EnName { get; set; }
    
    public string ZhName { get; set; }
    
    public string Address { get; set; }
    
    public string Latitude { get; set; }
    
    public string Longitude { get; set; }
    
    public string Link { get; set; }
    
    public string AppId { get; set; }
    
    public string AppSecret { get; set; }
    
    public bool IsLink { get; set; }
    
    public List<string> PhoneNumbers { get; set; }
}

public class UpdatePosCompanyStoreResponse : SmartiesResponse<PosCompanyStoreDto>;