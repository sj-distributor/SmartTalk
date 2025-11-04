using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartTalk.Messages.Dto.AutoTest;

public class AutoTestInputJsonDto
{
    [JsonConverter(typeof(EmptyStringConverter))]
    public string Recording { get; set; }

    [JsonConverter(typeof(EmptyStringConverter))]
    public string OrderId { get; set; }
    
    [JsonConverter(typeof(EmptyStringConverter))]
    public string CustomerId { get; set; }
    
    public List<AutoTestInputDetail> Detail { get; set; } = new();
}

public class AutoTestInputDetail
{
    [JsonConverter(typeof(EmptyStringConverter))]
    public int SerialNumber { get; set; }
    
    [JsonConverter(typeof(EmptyStringConverter))]
    public decimal Quantity { get; set; }
    
    [JsonConverter(typeof(EmptyStringConverter))]
    public string ItemDesc { get; set; }
}

public class EmptyStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetString() ?? "";

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value ?? "");
}
