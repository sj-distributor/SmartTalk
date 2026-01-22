using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.AISpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantDataProvider
{
    Task<AiSpeechAssistantTimer> GetAiSpeechAssistantTimerByAssistantIdAsync(int assistantId, CancellationToken cancellationToken = default);
}

public partial class AiSpeechAssistantDataProvider
{
    public async Task<AiSpeechAssistantTimer> GetAiSpeechAssistantTimerByAssistantIdAsync(int assistantId, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<AiSpeechAssistantTimer>().Where(x => x.AssistantId == assistantId)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}