using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SmartTalk.Messages.Enums.Translation;

[JsonConverter(typeof(StringEnumConverter))]
public enum TranslationLanguage
{
    [Description("en")]
    English,

    [Description("es")]
    Spanish,

    [Description("th")]
    Thai,

    [Description("vi")]
    Vietnamese,

    [Description("fil")]
    Filipino,

    [Description("hi")]
    Hindi
}
