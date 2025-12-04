using Serilog;
using SmartTalk.Core.Domain.Hr;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.Hr;
using SmartTalk.Messages.Enums.Hr;

namespace SmartTalk.Core.Services.Hr;

public interface IHrJobProcessJobService : IScopedDependency
{
    Task RefreshHrInterviewQuestionsCacheAsync(RefreshHrInterviewQuestionsCacheCommand command, CancellationToken cancellationToken);
}

public class HrJobProcessJobService : IHrJobProcessJobService
{
    private readonly IHrDataProvider _hrDataProvider;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    public HrJobProcessJobService(IHrDataProvider hrDataProvider, IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _hrDataProvider = hrDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
    }

    public async Task RefreshHrInterviewQuestionsCacheAsync(RefreshHrInterviewQuestionsCacheCommand command, CancellationToken cancellationToken)
    {
        var tasks = Enum.GetValues(typeof(HrInterviewQuestionSection))
            .Cast<HrInterviewQuestionSection>()
            .Select(section => ProcessSingleHrInterviewSectionQuestionsCacheAsync(section, cancellationToken))
            .ToList();

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        if (results.Length == 0) return;

        await RefreshVariableCacheAsync(results.Select(x => x.Cache).ToList(), cancellationToken).ConfigureAwait(false);
        
        await MarkHrInterviewQuestionsUsingStatusAsync(results.SelectMany(x => x.questions).ToList(), cancellationToken).ConfigureAwait(false);
    }

    private async Task<(List<HrInterviewQuestion> questions, AiSpeechAssistantKnowledgeVariableCache Cache)> ProcessSingleHrInterviewSectionQuestionsCacheAsync(HrInterviewQuestionSection section, CancellationToken cancellationToken)
    {
        var questions = await _hrDataProvider.GetHrInterviewQuestionsAsync(section, false, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (questions == null || questions.Count == 0) return ([], null);

        var randomQuestions = RandomPickHrInterviewQuestions(questions);

        var questionText = string.Join(Environment.NewLine, randomQuestions.Select((q, index) => $"{index + 1}. {q.Question}"));
        var cache = new AiSpeechAssistantKnowledgeVariableCache
        {
            CacheKey = "hr_interview_" + section.ToString().ToLower(),
            CacheValue = questionText,
            Filter = section.ToString()
        };
        
        Log.Information("Processed {section} questions, this time will pick these questions: {@RandomQuestions}, cache: {@Cache}", section, questionText, cache);
        
        return (randomQuestions, cache);
    }

    private static List<HrInterviewQuestion> RandomPickHrInterviewQuestions(List<HrInterviewQuestion> questions, int take = 10)
    {
        var random = new Random();
        return questions.OrderBy(x => random.Next()).Take(take).ToList();
    }

    private async Task RefreshVariableCacheAsync(List<AiSpeechAssistantKnowledgeVariableCache> newCaches, CancellationToken cancellationToken)
    {
        var cacheKeys = Enum.GetValues(typeof(HrInterviewQuestionSection))
            .Cast<HrInterviewQuestionSection>()
            .Select(section => "hr_interview_" + section.ToString().ToLower())
            .ToList();

        var caches = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeVariableCachesAsync(cacheKeys, cancellationToken).ConfigureAwait(false);

        if (caches.Count == 0)
        {
            await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantKnowledgeVariableCachesAsync(newCaches, true, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            foreach (var cache in newCaches)
            {
                var matchCache = caches.FirstOrDefault(x => x.CacheKey == cache.CacheKey);

                if (matchCache == null) continue;
                
                matchCache.CacheValue = cache.CacheValue;
            }
            
            await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantKnowledgeVariableCachesAsync(newCaches, true, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task MarkHrInterviewQuestionsUsingStatusAsync(List<HrInterviewQuestion> randomQuestions, CancellationToken cancellationToken)
    {
        randomQuestions.ForEach(x => x.IsUsing = true);
        
        var usingQuestions = await _hrDataProvider.GetHrInterviewQuestionsAsync(isUsing: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (usingQuestions.Count != 0)
        {
            usingQuestions.ForEach(question => question.IsUsing = false);
            randomQuestions.AddRange(usingQuestions);
        }
        
        await _hrDataProvider.UpdateHrInterviewQuestionsAsync(randomQuestions, true, cancellationToken).ConfigureAwait(false);
    }
}