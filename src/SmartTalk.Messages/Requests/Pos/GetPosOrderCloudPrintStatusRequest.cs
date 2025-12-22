using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetPosOrderCloudPrintStatusRequest : IRequest
{
    public int StoreId { get; set; }
    
    public int OrderId { get; set; }
}

public class GetPosOrderCloudPrintStatusResponse : SmartTalkResponse<GetPosOrderCloudPrintStatusDto>
{
}

public class GetPosOrderCloudPrintStatusDto
{
    public Guid? Id { get; set; }
    
    public CloudPrintStatus? CloudPrintStatus { get; set; }
    
    public bool? IsLink { get; set; }

    public bool? IsLinkCouldPrinting { get; set; }
}