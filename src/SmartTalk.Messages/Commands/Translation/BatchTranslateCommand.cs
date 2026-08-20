using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.Translation;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Translation;

public class BatchTranslateCommand : ICommand
{
    public string SourceLanguage { get; set; }

    public List<TranslationLanguage> TargetLanguages { get; set; } = [];

    public List<BatchTranslateItemDto> Items { get; set; } = [];

    public string Context { get; set; }
}

public class BatchTranslateItemDto
{
    public string Id { get; set; }

    public string Text { get; set; }
}

public class BatchTranslateResponse : SmartTalkResponse<BatchTranslateResponseData>
{
}

public class BatchTranslateResponseData
{
    public string Model { get; set; }

    public string SourceLanguage { get; set; }

    public List<TranslationLanguage> TargetLanguages { get; set; } = [];

    public List<BatchTranslateResultDto> Items { get; set; } = [];
}

public class BatchTranslateResultDto
{
    public string Id { get; set; }

    public string SourceText { get; set; }

    public Dictionary<string, string> Translations { get; set; } = new();
}
