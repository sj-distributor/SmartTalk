using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class UpdateCompanyStatusCommand : ICommand
{
    public int Id { get; set; }
    
    public bool Status { get; set; }
}

public class UpdateCompanyStatusResponse : SmartTalkResponse<CompanyDto>
{
}