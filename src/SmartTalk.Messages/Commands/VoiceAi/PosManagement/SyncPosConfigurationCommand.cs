using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.VoiceAi.PosManagement;

public class SyncPosConfigurationCommand : ICommand
{
    public int StoreId { get; set; }
    
    public int UserId { get; set; } 
}

public class SyncPosConfigurationResponse : SmartTalkResponse<EasyPosResponseDto>
{
}