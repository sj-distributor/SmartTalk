using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetCompanyStoreDetailRequest : IRequest
{
    public int StoreId { get; set; }
}

public class GetCompanyStoreDetailResponse : SmartTalkResponse<CompanyStoreDto>;