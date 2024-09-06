using System.Net.Http.Headers;
using System.Text;

namespace SmartTalk.Core.Services.WebSocket;

public partial class Asterisk
{
    private HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        
        var byteArray = new UTF8Encoding().GetBytes($"{AriUser}:{AriPassword}");
        
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        
        return client;
    }
}