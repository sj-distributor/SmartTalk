namespace SmartTalk.Messages.Dto.WebSocket;

public class UploadResultDto
{
    public UploadResultData Data { get; set; }
}

public class UploadResultData
{
    public string FileUrl { get; set; }
}