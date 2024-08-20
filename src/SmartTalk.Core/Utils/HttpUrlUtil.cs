namespace SmartTalk.Core.Utils;

public static class HttpUrlUtil
{
    public static string ReplaceHttpWithHttps(string httpUrl)
    {
        if (string.IsNullOrEmpty(httpUrl)) return null;

        if (httpUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return "https://" + httpUrl[7..];
        
        return httpUrl;
    }
}