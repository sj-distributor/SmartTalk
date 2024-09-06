using System.Text.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace SmartTalk.Messages.Dto.Embedding;

public struct EmbeddingDto : IEquatable<EmbeddingDto>
{
    [JsonIgnore] public ReadOnlyMemory<float> Data { get; set; } = new();

    [JsonIgnore] public int Length => Data.Length;

    public EmbeddingDto(float[] vector)
    {
        Data = vector;
    }

    public EmbeddingDto(ReadOnlyMemory<float> vector)
    {
        Data = vector;
    }

    public EmbeddingDto(int size)
    {
        Data = new ReadOnlyMemory<float>(new float[size]);
    }

    public static implicit operator EmbeddingDto(ReadOnlyMemory<float> data) => new(data);

    public static implicit operator EmbeddingDto(float[] data) => new(data);

    public bool Equals(EmbeddingDto other) => Data.Equals(other.Data);

    public override bool Equals(object obj) => (obj is EmbeddingDto other && Equals(other));

    public static bool operator ==(EmbeddingDto v1, EmbeddingDto v2) => v1.Equals(v2);

    public static bool operator !=(EmbeddingDto v1, EmbeddingDto v2) => !(v1 == v2);

    public override int GetHashCode() => Data.GetHashCode();
    
    public byte[] VectorBlob() => Data.ToArray().SelectMany(BitConverter.GetBytes).ToArray();

    public sealed class JsonConverter : JsonConverter<EmbeddingDto>
    {
        private static readonly JsonConverter<float[]> s_converter =
            (JsonConverter<float[]>)new JsonSerializerOptions().GetConverter(typeof(float[]));

        public override EmbeddingDto Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new EmbeddingDto(s_converter.Read(ref reader, typeof(float[]), options) ?? Array.Empty<float>());
        }

        public override void Write(Utf8JsonWriter writer, EmbeddingDto value, JsonSerializerOptions options)
        {
            s_converter.Write(
                writer, MemoryMarshal.TryGetArray(value.Data, out ArraySegment<float> array) && array.Count == value.Length
                    ? array.Array!
                    : value.Data.ToArray(), options);
        }
    }
}