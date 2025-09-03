using SmartTalk.Messages.Enums.STT;

namespace SmartTalk.Messages.Dto.PhoneOrder;

public class PhoneOrderRecordReportDto
{
    public int Id { get; set; }

    public int RecordId { get; set; }

    public TranscriptionLanguage Language { get; set; }

    public string Report { get; set; }
   
    public DateTimeOffset CreatedDate { get; set; }
}