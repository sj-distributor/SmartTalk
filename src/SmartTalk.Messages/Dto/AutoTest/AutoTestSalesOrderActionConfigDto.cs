using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.AutoTest;

public class AutoTestSalesOrderActionConfigDto
{
    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("headers")]
    public string Headers { get; set; }

    [JsonProperty("http_method")]
    public string HttpMethod { get; set; }

    [JsonProperty("body")]
    public string Body { get; set; }
}