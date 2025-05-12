using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.VoiceAi.PosManagement;

public class UpdatePosCategoryCommand : ICommand
{
    public int Id { get; set; }
    
    public string Names { get; set; }
}

public class UpdatePosCategoryResponse : SmartTalkResponse<List<PosCategoryDto>>
{
}