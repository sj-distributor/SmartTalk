using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Requests.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class CreateCompanyCommand : HasServiceProviderId, ICommand
{
    public string Name { get; set; }

    public string Description { get; set; }
}

public class CreateCompanyResponse : SmartTalkResponse<CompanyDto>
{
}