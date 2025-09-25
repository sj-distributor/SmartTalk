namespace SmartTalk.Messages.Dto.HrInterView;

public class HrInterViewSessionDto
{
    public int Id { get; set; }

    public Guid SessionId { get; set; }

    public string Message { get; set; }

    public string Bytes { get; set; }

    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
}