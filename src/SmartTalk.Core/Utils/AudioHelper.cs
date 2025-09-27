using System.Reflection;
using System.Security.Cryptography;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Utils;

public static class AudioHelper
{
    private static readonly string[] AllowedExtensions = [".wav"];

    public static Stream GetRandomAudioStream(AiSpeechAssistantVoice voice, AiSpeechAssistantMainLanguage language)
    {
        var voiceName = voice.ToString();
        var languageName = language.ToString();

        var resourcePrefix = $"Assets.Audio.RepeatOrderHoldon.{voiceName}.{languageName}";

        var assembly = Assembly.GetExecutingAssembly();
        
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase) && AllowedExtensions.Contains(Path.GetExtension(name)))
            .ToArray();

        if (resourceNames.Length == 0)
            throw new InvalidOperationException($"没有找到音频资源：{voiceName} - {languageName}");

        var randomIndex = RandomNumberGenerator.GetInt32(resourceNames.Length);
        var selectedResource = resourceNames[randomIndex];

        return assembly.GetManifestResourceStream(selectedResource);
    }
}