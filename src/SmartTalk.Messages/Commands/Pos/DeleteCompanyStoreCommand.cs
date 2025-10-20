using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class DeleteCompanyStoreCommand : ICommand
{
    public int StoreId { get; set; }
}

public class DeleteCompanyStoreResponse : SmartTalkResponse<List<CompanyStoreDto>>;