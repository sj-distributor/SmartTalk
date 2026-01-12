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
    
    [JsonConverter(typeof(EmptyStringConverter))]
    public string PromptText { get; set; }
    
    public DateTime OrderDate { get; set; } 
    
    public List<AutoTestInputDetail> Detail { get; set; } = new();
}

public class AutoTestInputDetail
{
    public int SerialNumber { get; set; }
    
    public decimal Quantity { get; set; }
    
    public string Unit { get; set; }
    
    [JsonConverter(typeof(EmptyStringConverter))]
    public string ItemName { get; set; }
    
    [JsonConverter(typeof(EmptyStringConverter))]
    public string ItemId { get; set; }
}

public class EmptyStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetString() ?? "";

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value ?? "");
}
