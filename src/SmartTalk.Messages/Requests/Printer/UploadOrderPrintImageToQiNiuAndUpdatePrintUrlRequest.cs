using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Requests.Printer;

public class UploadOrderPrintImageToQiNiuAndUpdatePrintUrlRequest : IRequest
{
    public Guid JobToken { get; set; }

    public DateTimeOffset PrintDate = DateTimeOffset.Now;
}

public class UploadOrderPrintImageToQiNiuAndUpdatePrintUrlResponse : IResponse
{
    public string ImageUrl { get; set; }
}
