using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;

namespace SmartTalk.Messages.Requests.VoiceAi.PosManagement;

public class GetCompanyStorePosRequest : IRequest
{
    public int StoreId { get; set; }
}

public class GetCompanyStorePosResponse : SmartiesResponse<PosCompanyStoreDto>
{
}