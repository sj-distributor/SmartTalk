using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Requests.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class CreatePosCompanyCommand : PosHasServiceId, ICommand
{
    public string Name { get; set; }

    public string Description { get; set; }
}

public class CreatePosCompanyResponse : SmartTalkResponse<PosCompanyDto>
{
}