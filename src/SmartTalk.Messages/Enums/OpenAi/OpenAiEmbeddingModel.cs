using System.ComponentModel;

namespace SmartTalk.Messages.Enums.OpenAi;

public enum OpenAiEmbeddingModel
{
    [Description("text-embedding-ada-002")]
    TextEmbeddingAda002,
    
    [Description("text-embedding-3-small")]
    TextEmbedding3Small,
    
    [Description("text-embedding-3-large")]
    TextEmbedding3Large
}