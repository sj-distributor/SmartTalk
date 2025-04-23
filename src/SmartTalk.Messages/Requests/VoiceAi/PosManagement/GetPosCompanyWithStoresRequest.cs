using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.VoiceAi.PosManagement;

public class GetPosCompanyWithStoresRequest : IRequest
{
    public int? PageIndex { get; set; }
    
    public int? PageSize { get; set; }
    
    public string Keyword { get; set; }
}

public class GetPosCompanyWithStoresResponse : SmartTalkResponse<GetPosCompanyWithStoresResponseData>;

public class GetPosCompanyWithStoresResponseData
{
    public int Count { get; set; }
    
    public List<GetPosCompanyWithStoresData> Data { get; set; }
}

public class GetPosCompanyWithStoresData
{
    public int Count { get; set; }
    
    public PosCompanyDto Company { get; set; }
    
    public List<PosCompanyStoreDto> Stores { get; set; }
}
