using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class DeleteCompanyCommand : ICommand
{
    public int Id { get; set; }
}

public class DeleteCompanyResponse : SmartTalkResponse<CompanyDto>
{
}