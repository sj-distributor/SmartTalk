using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Messages.Enums.KnowledgeScenario;
using Xunit;

namespace SmartTalk.UnitTests.Services.KnowledgeScenario;

public class AutoAddLanguageCatalogTests
{
    [Fact]
    public void AutoAddLanguage_ContainsAllFixedPageLanguages()
    {
        var languages = Enum.GetValues<AutoAddLanguage>();

        Assert.Contains(AutoAddLanguage.Mandarin, languages);
        Assert.Contains(AutoAddLanguage.Cantonese, languages);
        Assert.Contains(AutoAddLanguage.Spanish, languages);
        Assert.Contains(AutoAddLanguage.Korean, languages);
        Assert.Contains(AutoAddLanguage.Vietnamese, languages);
        Assert.Contains(AutoAddLanguage.Thai, languages);
    }
}

public class CrmToAutoAddLanguageConverterTests
{
    [Theory]
    [InlineData("国语", "Mandarin")]
    [InlineData("中文（大陸）", "Mandarin")]
    [InlineData("Cantonese", "Cantonese")]
    [InlineData("Spanish", "Spanish")]
    public void ResolveLanguageCodeOrDefault_NormalizesCrmLanguage(string rawLanguage, string expected)
    {
        Assert.Equal(expected, CrmToAutoAddLanguageConverter.ResolveLanguageCodeOrDefault(rawLanguage));
    }
}
