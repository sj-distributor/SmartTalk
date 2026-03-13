using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Requests.AutoTest;

public class QueryRecordingDataRequest : IRequest
{
    public List<string> CustomerId { get; set; } = new ();
    
    public DateTime StartDate { get; set; }
    
    public DateTime EndDate { get; set; }
    
    public int PageNumber { get; set; } = 1;
    
    public int PageSize { get; set; } = 200;
}

public class QueryRecordingDataResponse : IResponse
{
    public RecordingDataWrapper Data { get; set; }
}

public class RecordingDataWrapper
{
    public List<RecordingDataItem> RecordingData { get; set; }
}

public class RecordingDataItem
{
    public string CustomerId { get; set; }

    public string SalesDocument { get; set; }
    
    public string Material { get; set; }
    
    public string Description { get; set; }

    public decimal Qty { get; set; }
}