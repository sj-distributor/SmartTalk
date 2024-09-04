using System.Text.Json.Serialization;
using Smarties.Messages.DTO.RetrievalDb;
using SmartTalk.Messages.Dto.Embedding;

namespace SmartTalk.Messages.Dto.RetrievalDb.VectorDb;

public class VectorRecordDto
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("vector")]
    [JsonPropertyOrder(100)]
    [JsonConverter(typeof(EmbeddingDto.JsonConverter))]
    public EmbeddingDto Vector { get; set; } = new();

    [JsonPropertyName("tags")]
    [JsonPropertyOrder(2)]
    public TagCollectionDto Tags { get; set; } = new();

    [JsonPropertyName("payload")]
    [JsonPropertyOrder(3)]
    public Dictionary<string, object> Payload { get; set; } = new();
}