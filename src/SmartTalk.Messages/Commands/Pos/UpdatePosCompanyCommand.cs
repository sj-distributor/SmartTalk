using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class UpdatePosCompanyCommand : ICommand
{
    public int Id { get; set; }
    
    public string Name { get; set; }

    public string Description { get; set; }
}

public class UpdatePosCompanyResponse : SmartTalkResponse<PosCompanyDto>
{
}