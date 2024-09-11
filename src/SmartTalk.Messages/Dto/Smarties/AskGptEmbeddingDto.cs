using SmartTalk.Messages.Dto.OpenAi;
using SmartTalk.Messages.Enums.OpenAi;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Dto.Smarties;

public class AskGptEmbeddingRequestDto
{
    public string Input { get; set; }

    public OpenAiEmbeddingModel Model { get; set; } = OpenAiEmbeddingModel.TextEmbedding3Large;
}

public class AskGptEmbeddingResponseDto : SmartTalkResponse<OpenAiEmbeddingResponseDto>
{
}