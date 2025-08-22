using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class SyncPosConfigurationCommand : ICommand
{
    public int StoreId { get; set; }
}

public class SyncPosConfigurationResponse : SmartTalkResponse<List<PosProductDto>>
{
}