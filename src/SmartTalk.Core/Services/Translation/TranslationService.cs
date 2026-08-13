using System.ClientModel;
using System.Text.Json;
using OpenAI;
using OpenAI.Chat;
using Serilog;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Commands.Translation;
using SmartTalk.Messages.Enums.Translation;

namespace SmartTalk.Core.Services.Translation;

public interface ITranslationService : IScopedDependency
{
    Task<BatchTranslateResponse> BatchTranslateAsync(BatchTranslateCommand command, CancellationToken cancellationToken);
}

public class TranslationService : ITranslationService
{
    private const string TranslationModel = "gpt-5-mini";
    private readonly OpenAiSettings _openAiSettings;

    public TranslationService(OpenAiSettings openAiSettings)
    {
        _openAiSettings = openAiSettings;
    }

    public async Task<BatchTranslateResponse> BatchTranslateAsync(BatchTranslateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var targetLanguages = NormalizeLanguages(command.TargetLanguages);
        var targetLanguageCodes = targetLanguages.Select(language => language.GetDescription()).ToList();
        var items = NormalizeItems(command.Items);

        if (targetLanguages.Count == 0)
            throw new InvalidOperationException("Target languages cannot be empty.");

        if (items.Count == 0)
            throw new InvalidOperationException("Translation items cannot be empty.");

        if (items.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != items.Count)
            throw new InvalidOperationException("Translation item ids must be unique.");

        if (string.IsNullOrWhiteSpace(_openAiSettings.ApiKey))
            throw new InvalidOperationException("OpenAI API key is not configured.");

        var client = CreateChatClient();
        var payload = JsonSerializer.Serialize(new
        {
            sourceLanguage = string.IsNullOrWhiteSpace(command.SourceLanguage) ? "auto" : command.SourceLanguage.Trim(),
            targetLanguages = targetLanguageCodes,
            context = command.Context,
            items = items.Select(item => new
            {
                id = item.Id,
                text = item.Text
            })
        });

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                "You are a translation API. Translate restaurant, grocery, order, menu, and customer-service text accurately. " +
                "Preserve numbers, units, SKU names, brand names, punctuation meaning, JSON safety, and placeholders such as {name}, {{name}}, %s, and <tag>. " +
                "Do not add explanations. Return only valid JSON with top-level field \"items\". " +
                "Each item must contain id, sourceText, and translations. translations must be an object whose keys exactly match the requested targetLanguages."),
            new UserChatMessage(payload)
        };

        var completion = await client
            .CompleteChatAsync(messages, new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            }, cancellationToken)
            .ConfigureAwait(false);

        var jsonResponse = completion.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
        Log.Information("Batch translation response: {TranslationResponse}", jsonResponse);

        var results = ParseTranslationResults(jsonResponse, items, targetLanguageCodes);

        return new BatchTranslateResponse
        {
            Data = new BatchTranslateResponseData
            {
                Model = TranslationModel,
                SourceLanguage = string.IsNullOrWhiteSpace(command.SourceLanguage) ? "auto" : command.SourceLanguage.Trim(),
                TargetLanguages = targetLanguages,
                Items = results
            }
        };
    }

    private ChatClient CreateChatClient()
    {
        var options = new OpenAIClientOptions
        {
            NetworkTimeout = TimeSpan.FromMinutes(2)
        };

        return new ChatClient(TranslationModel, new ApiKeyCredential(_openAiSettings.ApiKey), options);
    }

    private static List<TranslationLanguage> NormalizeLanguages(IEnumerable<TranslationLanguage> languages)
    {
        return languages?
            .Distinct()
            .ToList() ?? [];
    }

    private static List<BatchTranslateItemDto> NormalizeItems(IEnumerable<BatchTranslateItemDto> items)
    {
        return items?
            .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id) && item.Text != null)
            .Select(item => new BatchTranslateItemDto
            {
                Id = item.Id.Trim(),
                Text = item.Text
            })
            .ToList() ?? [];
    }

    private static List<BatchTranslateResultDto> ParseTranslationResults(string jsonResponse, IReadOnlyCollection<BatchTranslateItemDto> sourceItems, IReadOnlyCollection<string> targetLanguages)
    {
        if (string.IsNullOrWhiteSpace(jsonResponse))
            throw new InvalidOperationException("OpenAI returned an empty translation response.");

        using var jsonDocument = JsonDocument.Parse(jsonResponse);

        if (!jsonDocument.RootElement.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("OpenAI translation response is missing items array.");

        var sourceById = sourceItems.ToDictionary(item => item.Id, item => item.Text, StringComparer.Ordinal);
        var resultsById = new Dictionary<string, BatchTranslateResultDto>(StringComparer.Ordinal);

        foreach (var itemElement in itemsElement.EnumerateArray())
        {
            var id = GetStringProperty(itemElement, "id");
            if (string.IsNullOrWhiteSpace(id) || !sourceById.TryGetValue(id, out var sourceText))
                continue;

            var translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (itemElement.TryGetProperty("translations", out var translationsElement) && translationsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var language in targetLanguages)
                {
                    translations[language] = translationsElement.TryGetProperty(language, out var translatedText)
                        ? translatedText.GetString() ?? string.Empty
                        : string.Empty;
                }
            }

            resultsById[id] = new BatchTranslateResultDto
            {
                Id = id,
                SourceText = GetStringProperty(itemElement, "sourceText") ?? sourceText,
                Translations = EnsureTargetLanguages(translations, targetLanguages)
            };
        }

        return sourceItems
            .Select(item => resultsById.TryGetValue(item.Id, out var result)
                ? result
                : new BatchTranslateResultDto
                {
                    Id = item.Id,
                    SourceText = item.Text,
                    Translations = EnsureTargetLanguages(new Dictionary<string, string>(), targetLanguages)
                })
            .ToList();
    }

    private static string GetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static Dictionary<string, string> EnsureTargetLanguages(Dictionary<string, string> translations, IEnumerable<string> targetLanguages)
    {
        foreach (var language in targetLanguages)
        {
            translations.TryAdd(language, string.Empty);
        }

        return translations;
    }
}
