namespace SmartTalk.Messages.Dto.Attachments;

public class UploadAttachmentDto
{
    public Guid Uuid { get; } = Guid.NewGuid();
    
    public string FileName { get; set; }
    
    public byte[] FileContent { get; set; }
    
    /// <summary>
    /// 文件索引位置，例如传：20230131，最终文件会在 20230131/{FileName}
    /// </summary>
    public string FileIndex { get; set; } = $"{DateTimeOffset.Now:yyyyMMdd}";
}