using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.OpenAi;

public class OpenAiEmbeddingResponseDto
{
    [JsonProperty("object")]
    public string Object { get; set; }
    
    [JsonProperty("data")]
    public List<OpenAiEmbeddingDataDto> Data { get; set; }
    
    [JsonProperty("model")]
    public string Model { get; set; }
    
    [JsonProperty("usage")]
    public OpenAiEmbeddingUsageDto Usage { get; set; }
}

public class OpenAiEmbeddingDataDto
{
    [JsonProperty("object")]
    public string Object { get; set; }
    
    [JsonProperty("index")]
    public int Index { get; set; }
    
    [JsonProperty("embedding")]
    public List<float> Embedding { get; set; }
}

public class OpenAiEmbeddingUsageDto
{
    [JsonProperty("prompt_tokens")]
    public int PromptTokens { get; set; }
    
    [JsonProperty("total_tokens")]
    public int TotalTokens { get; set; }
}