using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.VoiceAi.PosManagement;

public class GetPosCompanyStoreDetailRequest : IRequest
{
    public int StoreId { get; set; }
}

public class GetPosCompanyStoreDetailResponse : SmartTalkResponse<PosCompanyStoreDto>;