using System.Net.Http.Headers;
using SmartTalk.Messages.Dto.WebSocket;

namespace SmartTalk.Core.Services.WebSocket;

public partial class Asterisk
{
    private async Task<string> UploadFileAsync(byte[] file, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient();
        
        client.DefaultRequestHeaders.Add("Authorization", "xxx");

        using var form = new MultipartFormDataContent();
        
        var fileContent = new ByteArrayContent(file);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");
        form.Add(fileContent, "file", Guid.NewGuid() + ".wav");
        
        var response = await client.PostAsync($"https://testsmarties.yamimeal.ca/api/Attachment/upload", form, cancellationToken).ConfigureAwait(false);
        var upload = await response.Content.ReadAsAsync<UploadResultDto>(cancellationToken).ConfigureAwait(false);
        
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("File uploaded successfully.");
        }
        else
        {
            Console.WriteLine($"File upload failed. Status code: {response.StatusCode}");
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"Response body: {responseBody}");
        }
        
        return upload.Data.FileUrl;
    }
}