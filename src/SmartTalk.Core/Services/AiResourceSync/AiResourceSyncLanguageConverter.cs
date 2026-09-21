using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Core.Services.KnowledgeScenario;

public static class AiResourceSyncLanguageConverter
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

        var token = rawLanguage.Trim();
        if (AliasLookup.TryGetValue(token, out language))
            return true;

        return Enum.TryParse(token, true, out language);
    }

    public static string NormalizeToken(string rawLanguage)
    {
        if (TryResolve(rawLanguage, out var language))
            return language.ToString();

        return string.IsNullOrWhiteSpace(rawLanguage)
            ? AutoAddLanguage.English.ToString()
            : rawLanguage.Trim();
    }

    public static string ToModelLanguage(string rawLanguage)
    {
        var languageToken = rawLanguage?
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (!TryResolve(languageToken, out var language))
            return "en";

        return language switch
        {
            AutoAddLanguage.Chinese => "Zh",
            AutoAddLanguage.English => "En",
            AutoAddLanguage.Spanish => "Spanish",
            AutoAddLanguage.Korean => "Korean",
            AutoAddLanguage.Japanese => "Japanese",
            AutoAddLanguage.Vietnamese => "Viet",
            AutoAddLanguage.Thai => "Thai",
            _ => "en"
        };
    }
}
