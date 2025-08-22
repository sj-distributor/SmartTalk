using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.Printer;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Printer;

public class GetMerchPrinterLogRequest : IRequest
{
    public int AgentId { get; set; }
    
    public string PrinterMac { get; set; }

    public DateTimeOffset? StartDate { get; set; }

    public DateTimeOffset? EndDate { get; set; }

    public int? Code { get; set; }

    public PrintLogType? PrintLogType { get; set; }
        
    public int PageIndex { get; set; }

    public int PageSize { get; set; } = 50;
}

public class GetMerchPrinterLogResponse : SmartTalkResponse<MerchPrinterLogCountDto>
{
}

public class MerchPrinterLogCountDto
{
    public int TotalCount { get; set; }

    public IEnumerable<MerchPrinterLogDto> MerchPrinterLogDtos { get; set; } = Enumerable.Empty<MerchPrinterLogDto>();
}

public class MerchPrinterLogDto
{
    public string LogType { get; set; }

    public string Event { get; set; }

    public int? Code { get; set; }

    public string CodeDescription { get; set; }

    public DateTimeOffset Time { get; set; }
}