using Aliyun.OSS;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.AliYun;

namespace SmartTalk.Core.Services.AliYun;

public interface IAliYunOssService : IScopedDependency
{
    string GetFileUrl(string fileName);

    Task<byte[]> GetFileByteArray(string fileName);

    void UploadFile(string fileName, byte[] fileContent);
}

public class AliYunOssService : IAliYunOssService
{
    private readonly OssClient _ossClient;
    private readonly AliYunSettings _aliYunSettings;

    public AliYunOssService(OssClient ossClient, AliYunSettings aliYunSettings)
    {
        _ossClient = ossClient;
        _aliYunSettings = aliYunSettings;
    }

    public string GetFileUrl(string fileName)
    {
        return _ossClient.GeneratePresignedUri(_aliYunSettings.OssBucketName, fileName, DateTime.MaxValue).AbsoluteUri;
    }

    public async Task<byte[]> GetFileByteArray(string fileName)
    {
        var file = _ossClient.GetObject(_aliYunSettings.OssBucketName, fileName);

        if (file == null) return null;

        await using var stream = new MemoryStream();
        await file.Content.CopyToAsync(stream);

        return stream.ToArray();
    }

    public void UploadFile(string fileName, byte[] fileContent)
    {
        _ossClient.PutObject(_aliYunSettings.OssBucketName, fileName, new MemoryStream(fileContent));
    }
    
    public static string SplitFileUrl(string mediaUrl)
    {
        var uri = new Uri(mediaUrl);
        
        return uri.AbsolutePath.TrimStart('/');
    }
}