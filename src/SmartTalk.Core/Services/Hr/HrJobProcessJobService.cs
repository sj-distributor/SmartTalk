using Serilog;
using System.Text;
using Enum = System.Enum;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Domain.Hr;
using SmartTalk.Messages.Enums.Hr;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Messages.Commands.Hr;
using SmartTalk.Core.Services.AiSpeechAssistant;

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
    
        var processResult = ProcessHrInterviewQuestionsCache(noUsingQuestions);
        if (processResult.Caches.Count == 0) return;
    
        await RefreshVariableCacheAsync(processResult.Caches, cancellationToken).ConfigureAwait(false);
    
        await MarkHrInterviewQuestionsUsingStatusAsync(processResult.PickedQuestions, cancellationToken).ConfigureAwait(false);
    }

    public static HrInterviewQuestionsCacheProcessResult ProcessHrInterviewQuestionsCache(List<HrInterviewQuestion> questions)
    {
        if (questions == null || questions.Count == 0) return new HrInterviewQuestionsCacheProcessResult();

        var random = new Random();

        var sections = Enum.GetValues(typeof(HrInterviewQuestionSection))
            .Cast<HrInterviewQuestionSection>()
            .ToList();

        var caches = new List<AiSpeechAssistantKnowledgeVariableCache>();
        var pickedBySection = new Dictionary<HrInterviewQuestionSection, List<HrInterviewQuestion>>();

        foreach (var section in sections)
        {
            var take = GetSectionTakeCount(section);

            var questionInSection = questions.Where(x => x.Section == section).ToList();
            var randomQuestions = RandomPickHrInterviewQuestions(questionInSection, random, take);

            pickedBySection[section] = randomQuestions;

            Log.Information("Random pick questions: {@Questions}", randomQuestions);

            var questionText = string.Join(
                Environment.NewLine,
                randomQuestions.Select((q, index) => $"{index + 1}. {q.Question}")
            );

            var cache = new AiSpeechAssistantKnowledgeVariableCache
            {
                CacheKey = "hr_interview_" + section.ToString().ToLower(),
                CacheValue = questionText,
                Filter = section.ToString()
            };

            Log.Information(
                "Processed {section} questions, this time will pick these questions: {@RandomQuestions}, cache: {@Cache}",
                section, questionText, cache);

            caches.Add(cache);
        }

        var allPickedDistinct = pickedBySection
            .SelectMany(kv => kv.Value)
            .DistinctBy(q => q.Id)
            .ToList();

        var mergedCache = new AiSpeechAssistantKnowledgeVariableCache
        {
            CacheKey = "hr_interview_questions",
            CacheValue = BuildMergedHrInterviewQuestionsText(pickedBySection),
            Filter = "all_sections"
        };

        caches.Add(mergedCache);

        return new HrInterviewQuestionsCacheProcessResult
        {
            Caches = caches,
            PickedQuestions = allPickedDistinct
        };
    }
    
    private static int GetSectionTakeCount(HrInterviewQuestionSection section) => section switch
    {
        HrInterviewQuestionSection.Section1 => 3,
        HrInterviewQuestionSection.Section2 => 2,
        HrInterviewQuestionSection.Section3 => 3,
        _ => 3
    };

    public static List<HrInterviewQuestion> RandomPickHrInterviewQuestions(
        List<HrInterviewQuestion> questions,
        Random random,
        int take)
    {
        if (questions == null || questions.Count == 0) return new();
        if (take <= 0) return new();

        return questions
            .OrderBy(_ => random.Next())
            .Take(Math.Min(take, questions.Count))
            .ToList();
    }
    
    private static string BuildMergedHrInterviewQuestionsText(
        Dictionary<HrInterviewQuestionSection, List<HrInterviewQuestion>> pickedBySection)
    {
        pickedBySection.TryGetValue(HrInterviewQuestionSection.Section1, out var s1);
        pickedBySection.TryGetValue(HrInterviewQuestionSection.Section2, out var s2);
        pickedBySection.TryGetValue(HrInterviewQuestionSection.Section3, out var s3);

        s1 ??= [];
        s2 ??= [];
        s3 ??= [];

        var sb = new StringBuilder();

        sb.AppendLine("Ask these questions one by one：");

        // Section1
        sb.AppendLine($"Let’s move on to {s1.Count} questions to learn a bit more about you:");
        var index = 1;
        foreach (var q in s1)
            sb.AppendLine($"{index++}. {q.Question}");
        sb.AppendLine();

        // Section2
        sb.AppendLine("The next few questions will give me a better idea of how you see things:");
        foreach (var q in s2)
            sb.AppendLine($"{index++}. {q.Question}");
        sb.AppendLine();

        // Section3
        sb.AppendLine("Let’s move into the discussion part now:");
        foreach (var q in s3)
            sb.AppendLine($"{index++}. {q.Question}");

        return sb.ToString().TrimEnd();
    }
    
    public async Task RefreshVariableCacheAsync(List<AiSpeechAssistantKnowledgeVariableCache> newCaches, CancellationToken cancellationToken)
    {
        if (newCaches == null || newCaches.Count == 0) return;

        var cacheKeys = newCaches.Select(x => x.CacheKey).Distinct().ToList();

        var existing = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantKnowledgeVariableCachesAsync(cacheKeys, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (existing.Count == 0)
        {
            await _aiSpeechAssistantDataProvider
                .AddAiSpeechAssistantKnowledgeVariableCachesAsync(newCaches, true, cancellationToken).ConfigureAwait(false);
            return;
        }

        var existingByKey = existing.ToDictionary(x => x.CacheKey, StringComparer.OrdinalIgnoreCase);

        var toAdd = new List<AiSpeechAssistantKnowledgeVariableCache>();
        var toUpdate = new List<AiSpeechAssistantKnowledgeVariableCache>();

        foreach (var cache in newCaches)
        {
            if (existingByKey.TryGetValue(cache.CacheKey, out var match))
            {
                match.CacheValue = cache.CacheValue;
                match.Filter = cache.Filter;
                toUpdate.Add(match);
            }
            else
                toAdd.Add(cache);
        }

        if (toUpdate.Count > 0)
            await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantKnowledgeVariableCachesAsync(toUpdate, true, cancellationToken).ConfigureAwait(false);

        if (toAdd.Count > 0)
            await _aiSpeechAssistantDataProvider.AddAiSpeechAssistantKnowledgeVariableCachesAsync(toAdd, true, cancellationToken).ConfigureAwait(false);
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
    
    public sealed class HrInterviewQuestionsCacheProcessResult
    {
        public List<AiSpeechAssistantKnowledgeVariableCache> Caches { get; init; } = [];
        
        public List<HrInterviewQuestion> PickedQuestions { get; init; } = [];
    }
}