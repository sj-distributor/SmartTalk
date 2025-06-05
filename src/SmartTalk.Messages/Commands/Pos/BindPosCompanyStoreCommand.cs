using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class BindPosCompanyStoreCommand : ICommand
{
    public int StoreId { get; set; }
    
    public string Link { get; set; }
    
    public string AppId { get; set; }
    
    public string AppSecret { get; set; }
}

public class BindPosCompanyStoreResponse : SmartTalkResponse<PosCompanyStoreDto>
{
}