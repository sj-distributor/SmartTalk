using Newtonsoft.Json;
using Shouldly;
using SmartTalk.Messages.Commands.Translation;
using SmartTalk.Messages.Enums.Translation;
using Xunit;

namespace SmartTalk.UnitTests.Utils;

public class TranslationLanguageSerializationTests
{
    [Fact]
    public void Deserialize_UsesLanguageCodesForSourceAndTargetLanguages()
    {
        const string json = "{\"sourceLanguage\":\"en\",\"targetLanguages\":[\"es\",\"zh-Hant\"]}";

        var command = JsonConvert.DeserializeObject<BatchTranslateCommand>(json);

        command.ShouldNotBeNull();
        command.SourceLanguage.ShouldBe("en");
        command.TargetLanguages.ShouldBe([TranslationLanguage.Spanish, TranslationLanguage.TraditionalChinese]);
    }

    [Fact]
    public void Serialize_WritesLanguageCodes()
    {
        var command = new BatchTranslateCommand
        {
            SourceLanguage = "auto",
            TargetLanguages = [TranslationLanguage.English, TranslationLanguage.Filipino]
        };

        var json = JsonConvert.SerializeObject(command);

        json.ShouldContain("\"sourceLanguage\":\"auto\"");
        json.ShouldContain("\"targetLanguages\":[\"en\",\"fil\"]");
    }
}
