using System.ComponentModel;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SmartTalk.Messages.Enums.Translation;

[JsonConverter(typeof(StringEnumConverter))]
public enum TranslationLanguage
{
    [EnumMember(Value = "en")]
    [Description("en")]
    English,

    [EnumMember(Value = "es")]
    [Description("es")]
    Spanish,

    [EnumMember(Value = "th")]
    [Description("th")]
    Thai,

    [EnumMember(Value = "vi")]
    [Description("vi")]
    Vietnamese,

    [EnumMember(Value = "fil")]
    [Description("fil")]
    Filipino,

    [EnumMember(Value = "hi")]
    [Description("hi")]
    Hindi,

    [EnumMember(Value = "zh-Hant")]
    [Description("zh-Hant")]
    TraditionalChinese
}
