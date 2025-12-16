using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.Pos;

namespace SmartTalk.Messages.Requests.Pos;

public class GetPosOrderCloudPrintStatusRequest : IRequest
{
    public int StoreId { get; set; }
    
    public int OrderId { get; set; }
}

public class GetPosOrderCloudPrintStatusResponse : IResponse
{
    public CloudPrintStatus CloudPrintStatus { get; set; }
}