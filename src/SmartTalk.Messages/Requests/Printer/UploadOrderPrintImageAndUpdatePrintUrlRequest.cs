using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Requests.Printer;

public class UploadOrderPrintImageAndUpdatePrintUrlRequest : IRequest
{
    public Guid JobToken { get; set; }

    public DateTimeOffset PrintDate = DateTimeOffset.Now;
}

public class UploadOrderPrintImageAndUpdatePrintUrlResponse : IResponse
{
    public string ImageUrl { get; set; }
}
