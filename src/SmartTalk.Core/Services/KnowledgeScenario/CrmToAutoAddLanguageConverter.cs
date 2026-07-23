using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Core.Services.KnowledgeScenario;

public static class CrmToAutoAddLanguageConverter
{
    private static readonly Dictionary<string, AutoAddLanguage> AliasLookup =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["中文"] = AutoAddLanguage.Chinese,
            ["英文"] = AutoAddLanguage.English,
            ["西文"] = AutoAddLanguage.Spanish,
            ["韓文"] = AutoAddLanguage.Korean,
            ["日文"] = AutoAddLanguage.Japanese,
            ["越南语"] = AutoAddLanguage.Vietnamese,
            ["泰国语"] = AutoAddLanguage.Thai,
        };

    public static bool TryResolve(string rawLanguage, out AutoAddLanguage language)
    {
        language = default;
        if (string.IsNullOrWhiteSpace(rawLanguage))
            return false;

        return AliasLookup.TryGetValue(rawLanguage.Trim(), out language);
    }

    public static string NormalizeToken(string rawLanguage)
    {
        if (TryResolve(rawLanguage, out var language))
            return language.ToString();

        return string.IsNullOrWhiteSpace(rawLanguage)
            ? AutoAddLanguage.English.ToString()
            : rawLanguage.Trim();
    }
}
