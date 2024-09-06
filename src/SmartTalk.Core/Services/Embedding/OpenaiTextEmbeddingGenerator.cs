using SmartTalk.Core.Settings.VectorDb;
using SmartTalk.Messages.Dto.Embedding;
using SmartTalk.Messages.Enums.OpenAi;
using Microsoft.KernelMemory.AI.OpenAI.GPT3;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Messages.Dto.Smarties;

namespace SmartTalk.Core.Services.Embedding;

public class OpenaiTextEmbeddingGenerator : ITextEmbeddingGenerator
{
    private readonly ISmartiesClient _smartiesClient;

    public OpenaiTextEmbeddingGenerator(VectorDbSettings vectorDbSettings, ISmartiesClient smartiesClient)
    {
        _smartiesClient = smartiesClient;
        MaxTokens = vectorDbSettings.EmbeddingModelMaxTokenTotal;
    }

    public int MaxTokens { get; }
    
    public int CountTokens(string text)
    {
        return GPT3Tokenizer.Encode(text).Count;
    }

    public async Task<EmbeddingDto> GenerateEmbeddingAsync(string text, OpenAiEmbeddingModel model = OpenAiEmbeddingModel.TextEmbedding3Large, CancellationToken cancellationToken = default)
    {
        var result = await _smartiesClient.GetEmbeddingAsync(
            new AskGptEmbeddingRequestDto
            {
                Input = text,
                Model = model
            }, cancellationToken).ConfigureAwait(false);

        return new EmbeddingDto(result.Data.Data.First().Embedding.ToArray());
    }
}