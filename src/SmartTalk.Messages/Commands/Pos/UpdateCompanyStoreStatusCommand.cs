using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class UpdateCompanyStoreStatusCommand : ICommand
{
    public int StoreId { get; set; }
    
    public bool Status { get; set; }
}

public class UpdateCompanyStoreStatusResponse : SmartTalkResponse<CompanyStoreDto>;