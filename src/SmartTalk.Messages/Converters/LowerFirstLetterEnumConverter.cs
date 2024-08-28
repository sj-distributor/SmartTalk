using Newtonsoft.Json;

namespace SmartTalk.Core.Utils;

public class LowerFirstLetterEnumConverter: JsonConverter
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
        var enumValue = (Enum)value;
        var stringValue = enumValue.ToString();
        var lowercasedStringValue = char.ToLower(stringValue[0]) + stringValue.Substring(1);
        writer.WriteValue(lowercasedStringValue);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var stringValue = (string)reader.Value;
        if (stringValue != null)
        {
            var uppercaseStringValue = char.ToUpper(stringValue[0]) + stringValue.Substring(1);
            var enumValue = Enum.Parse(_enumType, uppercaseStringValue);
            return enumValue;
        }
        throw new JsonSerializationException("Unexpected value when deserializing enum.");
    }
}