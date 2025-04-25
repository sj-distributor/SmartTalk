using Mediator.Net.Contracts;
using Smarties.Messages.Responses;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;

namespace SmartTalk.Messages.Requests.VoiceAi.PosManagement;

public class GetPosStoreUsersRequest : IRequest
{
    public int StoreId { get; set; }
}

public class GetPosStoreUsersResponse : SmartiesResponse<List<PosStoreUserDto>>
{
}