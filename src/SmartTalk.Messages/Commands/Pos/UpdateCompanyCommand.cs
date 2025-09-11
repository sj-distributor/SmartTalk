using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class UpdateCompanyCommand : ICommand
{
    public int Id { get; set; }
    
    public string Name { get; set; }

    public string Description { get; set; }
}

public class UpdateCompanyResponse : SmartTalkResponse<CompanyDto>
{
}