namespace SmartTalk.Messages.Dto.Attachments;

public class AttachmentDto
{
    public int Id { get; set; }
    
    public Guid Uuid { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
    
    public string FileUrl { get; set; }
    
    public string FileName { get; set; }

    public long FileSize { get; set; }
    
    public string FilePath { get; set; }
    
    public byte[] FileContent { get; set; }
    
    public string OriginFileName { get; set; }
}