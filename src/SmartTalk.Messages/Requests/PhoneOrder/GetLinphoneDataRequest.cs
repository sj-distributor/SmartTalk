using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

public class GetLinphoneDataRequest : IRequest
{
    public DateTime Time { get; set; }
}

public class GetLinphoneDataResponse : SmartTalkResponse<List<LinphoneData>>
{
}

public class LinphoneData
{
    public int CallId { get; set; }
    
    public string MerchName { get; set; }
    
    public string CallTime { get; set; }

    public string PickStat { get; set; }

    public string CallPeriod { get; set; }
}