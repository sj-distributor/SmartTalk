using Serilog;
using SmartTalk.Core.Domain.Hr;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.Hr;
using SmartTalk.Messages.Enums.Hr;
using Enum = System.Enum;

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
        var noUsingQuestions = await _hrDataProvider.GetHrInterviewQuestionsAsync(isUsing: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (noUsingQuestions.Count == 0) return;

        var randomResult = ProcessSingleHrInterviewSectionQuestionsCache(noUsingQuestions);
        if (randomResult.Count == 0) return;

        await RefreshVariableCacheAsync(randomResult.Select(x => x.Cache).ToList(), cancellationToken).ConfigureAwait(false);
        
        await MarkHrInterviewQuestionsUsingStatusAsync(randomResult.SelectMany(x => x.Questions).ToList(), cancellationToken).ConfigureAwait(false);
    }

    public static List<RandomHrInterviewQuestionCacheDto> ProcessSingleHrInterviewSectionQuestionsCache(List<HrInterviewQuestion> questions)
    {
        if (questions == null || questions.Count == 0) return [];

        var results = new List<RandomHrInterviewQuestionCacheDto>();
        foreach (var section in Enum.GetValues(typeof(HrInterviewQuestionSection)).Cast<HrInterviewQuestionSection>())
        {
            var questionInSection = questions.Where(x => x.Section == section).ToList();
            var randomQuestions = RandomPickHrInterviewQuestions(questionInSection);

            Log.Information("Random pick questions: {@Questions}", randomQuestions);
        
            var questionText = string.Join(Environment.NewLine, randomQuestions.Select((q, index) => $"{index + 1}. {q.Question}"));
            var cache = new AiSpeechAssistantKnowledgeVariableCache
            {
                CacheKey = "hr_interview_" + section.ToString().ToLower(),
                CacheValue = questionText,
                Filter = section.ToString()
            };
        
            Log.Information("Processed {section} questions, this time will pick these questions: {@RandomQuestions}, cache: {@Cache}", section, questionText, cache);
        
            results.Add(new RandomHrInterviewQuestionCacheDto() { Questions = randomQuestions, Cache = cache });
        }

        return results;
    }

    public static List<HrInterviewQuestion> RandomPickHrInterviewQuestions(List<HrInterviewQuestion> questions, int take = 3)
    {
        var random = new Random();
        return questions.OrderBy(x => random.Next()).Take(take).ToList();
    }

    public async Task RefreshVariableCacheAsync(List<AiSpeechAssistantKnowledgeVariableCache> newCaches, CancellationToken cancellationToken)
    {
        var cacheKeys = Enum.GetValues(typeof(HrInterviewQuestionSection))
            .Cast<HrInterviewQuestionSection>()
            .Select(section => "hr_interview_" + section.ToString().ToLower())
            .ToList();

        var caches = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeVariableCachesAsync(cacheKeys, cancellationToken: cancellationToken).ConfigureAwait(false);

        Log.Information("Fetching exist hr interview questions from caches: {@Caches}", caches);
        
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
            
            await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantKnowledgeVariableCachesAsync(caches, true, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task MarkHrInterviewQuestionsUsingStatusAsync(List<HrInterviewQuestion> randomQuestions, CancellationToken cancellationToken)
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
    
    public class RandomHrInterviewQuestionCacheDto
    {
        public List<HrInterviewQuestion> Questions { get; set; }
        
        public AiSpeechAssistantKnowledgeVariableCache Cache { get; set; } 
    }
}