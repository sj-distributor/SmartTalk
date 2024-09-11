using Newtonsoft.Json;
using SmartTalk.Messages.Extensions;

namespace SmartTalk.Messages.Converters;

public class LowerFirstLetterEnumConverter : JsonConverter
{
    private readonly Type _enumType;

    public LowerFirstLetterEnumConverter(Type enumType)
    {
        _enumType = enumType;
    }

    public override bool CanConvert(Type objectType)
    {
        return objectType == _enumType;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        writer.WriteValue(((Enum)value)?.ToString().ToCamelCase());
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var stringValue = (string)reader.Value;
        if (stringValue != null)
        {
            var enumValue = Enum.Parse(_enumType, stringValue.ToUpperFirstCase());
            return enumValue;
        }
        throw new JsonSerializationException("Unexpected value when deserializing enum.");
    }
}