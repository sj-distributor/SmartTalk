namespace SmartTalk.Messages.Dto.Aixvolink;

public class AixvolinkCallResultsCallbackRequest
{
    public DateTimeOffset CallTime { get; set; }

    public string CallerNumber { get; set; }

    public string CalleeNumber { get; set; }

    public int RecordId { get; set; }

    public string RecordingUrl { get; set; }
}
