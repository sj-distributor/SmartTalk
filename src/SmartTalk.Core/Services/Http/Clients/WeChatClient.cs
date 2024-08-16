using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.WeChat;
using SmartTalk.Messages.Enums.WeChat;

namespace SmartTalk.Core.Services.Http.Clients;

public interface IWeChatClient : IScopedDependency
{
    Task<WorkWeChatResponseDto> SendWorkWechatRobotMessagesAsync(string requestUrl, SendWorkWechatGroupRobotMessageDto messages, CancellationToken cancellationToken);
    
    Task<UploadWorkWechatTemporaryFileResponseDto> UploadWorkWechatTemporaryFileAsync(string accessToken, string fileName, UploadWorkWechatTemporaryFileType type, byte[] bytes, CancellationToken cancellationToken);
}

public class WeChatClient : IWeChatClient
{
    private readonly ISmartiesHttpClientFactory _httpClientFactory;

    public WeChatClient(ISmartiesHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }
    
    public async Task<WorkWeChatResponseDto> SendWorkWechatRobotMessagesAsync(
        string requestUrl, SendWorkWechatGroupRobotMessageDto messages, CancellationToken cancellationToken)
    {
        return await _httpClientFactory.PostAsJsonAsync<WorkWeChatResponseDto>(requestUrl, messages, cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<UploadWorkWechatTemporaryFileResponseDto> UploadWorkWechatTemporaryFileAsync(string accessToken, string fileName, UploadWorkWechatTemporaryFileType type, byte[] bytes, CancellationToken cancellationToken)
    {
        var boundary = DateTime.Now.Ticks.ToString("X");
        var content = new MultipartFormDataContent(boundary);
        content.Headers.Remove("Content-Type");
        content.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary=" + boundary);

        HttpContent byteContent = new ByteArrayContent(bytes);
        content.Add(byteContent);
        byteContent.Headers.Remove("Content-Type");
        byteContent.Headers.Remove("Content-Disposition");
        
        byteContent.Headers.TryAddWithoutValidation("Content-Type", "application/octet-stream");
        byteContent.Headers.TryAddWithoutValidation("Content-Disposition", $"form-data; name=\"media\";filename=\"{fileName}\";filelength={bytes.Length}");

        var sendUrl = $"https://qyapi.weixin.qq.com/cgi-bin/media/upload?access_token={accessToken}&type={type.ToString().ToLower()}";

        return await _httpClientFactory.PostAsync<UploadWorkWechatTemporaryFileResponseDto>(sendUrl, content, cancellationToken).ConfigureAwait(false);
    }
}