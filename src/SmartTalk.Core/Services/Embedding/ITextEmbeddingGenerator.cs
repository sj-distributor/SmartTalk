using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.Embedding;
using SmartTalk.Messages.Enums.OpenAi;

namespace SmartTalk.Core.Services.Embedding;

public interface ITextEmbeddingGenerator : IScopedDependency
{
    public int MaxTokens { get; }

    public int CountTokens(string text);

    public Task<EmbeddingDto> GenerateEmbeddingAsync(string text, OpenAiEmbeddingModel model = OpenAiEmbeddingModel.TextEmbedding3Large, CancellationToken cancellationToken = default);
}
