using SmartTalk.Core.Ioc;
using SmartTalk.Core.Data;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.AIAssistant;
using SmartTalk.Core.Domain.AISpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public interface IAiSpeechAssistantDataProvider : IScopedDependency
{
    Task<(Domain.AISpeechAssistant.AiSpeechAssistant, AiSpeechAssistantPromptTemplate, AiSpeechAssistantUserProfile)>
        GetAiSpeechAssistantInfoByNumbersAsync(string callerNumber, string didNumber, int? assistantId = null, CancellationToken cancellationToken = default);
    
    Task<Domain.AISpeechAssistant.AiSpeechAssistant> GetAiSpeechAssistantByNumbersAsync(string didNumber, CancellationToken cancellationToken);

    Task<AiSpeechAssistantHumanContact> GetAiSpeechAssistantHumanContactByAssistantIdAsync(int assistantId, CancellationToken cancellationToken);
    
    Task<List<AiSpeechAssistantFunctionCall>> GetAiSpeechAssistantFunctionCallByAssistantIdAsync(int assistantId, CancellationToken cancellationToken);
}

public class AiSpeechAssistantDataProvider : IAiSpeechAssistantDataProvider
{
    private readonly IRepository _repository;

    public AiSpeechAssistantDataProvider(IRepository repository)
    {
        _repository = repository;
    }

    public async Task<(Domain.AISpeechAssistant.AiSpeechAssistant, AiSpeechAssistantPromptTemplate, AiSpeechAssistantUserProfile)>
        GetAiSpeechAssistantInfoByNumbersAsync(string callerNumber, string didNumber, int? assistantId = null, CancellationToken cancellationToken = default)
    {
        var assistantInfo =
            from assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>()
            join promptTemplate in _repository.Query<AiSpeechAssistantPromptTemplate>() 
                on assistant.Id equals promptTemplate.AssistantId into promptGroup
            from promptTemplate in promptGroup.DefaultIfEmpty()
            join userProfile in _repository.Query<AiSpeechAssistantUserProfile>().Where(x => x.CallerNumber == callerNumber) 
                on assistant.Id equals userProfile.AssistantId into userProfileGroup
            from userProfile in userProfileGroup.DefaultIfEmpty()
            select new
            {
                assistant, promptTemplate, userProfile
            };

        assistantInfo = assistantInfo.Where(x => assistantId.HasValue ? x.assistant.Id == assistantId.Value : x.assistant.AnsweringNumber == didNumber);

        var result = await assistantInfo.FirstOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        return (result.assistant, result.promptTemplate, result.userProfile);
    }

    public async Task<Domain.AISpeechAssistant.AiSpeechAssistant> GetAiSpeechAssistantByNumbersAsync(string didNumber, CancellationToken cancellationToken)
    {
        return await _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>().Where(x => x.AnsweringNumber == didNumber)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiSpeechAssistantHumanContact> GetAiSpeechAssistantHumanContactByAssistantIdAsync(int assistantId, CancellationToken cancellationToken)
    {
        return await _repository.Query<AiSpeechAssistantHumanContact>().Where(x => x.AssistantId == assistantId)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AiSpeechAssistantFunctionCall>> GetAiSpeechAssistantFunctionCallByAssistantIdAsync(int assistantId, CancellationToken cancellationToken)
    {
        return await _repository.QueryNoTracking<AiSpeechAssistantFunctionCall>()
            .Where(x => x.AssistantId == assistantId).ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}