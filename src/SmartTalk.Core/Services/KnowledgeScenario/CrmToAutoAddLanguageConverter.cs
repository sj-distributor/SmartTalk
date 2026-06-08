using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Core.Services.KnowledgeScenario;

public static class CrmToAutoAddLanguageConverter
{
    private static readonly Dictionary<string, AutoAddLanguage> AliasLookup =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Mandarin"] = AutoAddLanguage.Mandarin,
            ["国语"] = AutoAddLanguage.Mandarin,
            ["國語"] = AutoAddLanguage.Mandarin,
            ["中文"] = AutoAddLanguage.Mandarin,
            ["中文（大陸）"] = AutoAddLanguage.Mandarin,
            ["中文大陸"] = AutoAddLanguage.Mandarin,
            ["Chinese"] = AutoAddLanguage.Mandarin,
            ["Zh"] = AutoAddLanguage.Mandarin,

            ["Cantonese"] = AutoAddLanguage.Cantonese,
            ["粤语"] = AutoAddLanguage.Cantonese,
            ["粵語"] = AutoAddLanguage.Cantonese,
            ["中文（香港）"] = AutoAddLanguage.Cantonese,
            ["中文香港"] = AutoAddLanguage.Cantonese,

            ["Spanish"] = AutoAddLanguage.Spanish,
            ["西班牙语"] = AutoAddLanguage.Spanish,
            ["西班牙語"] = AutoAddLanguage.Spanish,
            ["Es"] = AutoAddLanguage.Spanish,

            ["Korean"] = AutoAddLanguage.Korean,
            ["韩语"] = AutoAddLanguage.Korean,
            ["韓語"] = AutoAddLanguage.Korean,
            ["Ko"] = AutoAddLanguage.Korean,

            ["Vietnamese"] = AutoAddLanguage.Vietnamese,
            ["越南语"] = AutoAddLanguage.Vietnamese,
            ["越南語"] = AutoAddLanguage.Vietnamese,
            ["Viet"] = AutoAddLanguage.Vietnamese,
            ["Vi"] = AutoAddLanguage.Vietnamese,

            ["Thai"] = AutoAddLanguage.Thai,
            ["泰国语"] = AutoAddLanguage.Thai,
            ["泰國語"] = AutoAddLanguage.Thai,
            ["Th"] = AutoAddLanguage.Thai
        };

    public static bool TryResolve(string rawLanguage, out AutoAddLanguage language)
    {
        language = default;
        if (string.IsNullOrWhiteSpace(rawLanguage))
            return false;

        return AliasLookup.TryGetValue(rawLanguage.Trim(), out language);
    }

    public static string ResolveLanguageCodeOrDefault(string rawLanguage)
    {
        return TryResolve(rawLanguage, out var language)
            ? language.ToString()
            : rawLanguage?.Trim() ?? string.Empty;
    }

}
