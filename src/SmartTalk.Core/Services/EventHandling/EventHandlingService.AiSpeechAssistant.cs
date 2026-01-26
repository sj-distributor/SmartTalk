using DocumentFormat.OpenXml.Office2010.ExcelAc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using Smarties.Messages.DTO.OpenAi;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Events.AiSpeechAssistant;

namespace SmartTalk.Core.Services.EventHandling;

public partial class EventHandlingService
{
    public async Task HandlingEventAsync(AiSpeechAssistantKnowledgeAddedEvent @event, CancellationToken cancellationToken)
    {
        var prevKnowledgeCopyRelateds = @event.PrevKnowledge.KnowledgeCopyRelateds ?? new List<AiSpeechAssistantKnowledgeCopyRelatedDto>();
        var latestKnowledgeCopyRelateds = @event.LatestKnowledge.KnowledgeCopyRelateds ?? new List<AiSpeechAssistantKnowledgeCopyRelatedDto>();

        var oldMergedJsonObj = BuildMergedKnowledgeJson(@event.PrevKnowledge.Json, prevKnowledgeCopyRelateds.Select(x => x.CopyKnowledgePoints));
        var newMergedJsonObj = BuildMergedKnowledgeJson(@event.LatestKnowledge.Json, latestKnowledgeCopyRelateds.Select(x => x.CopyKnowledgePoints));

        Log.Information("Compare the knowledge: {@oldMergedJsonObj} and {@newMergedJsonObj}", oldMergedJsonObj, newMergedJsonObj);
        
        var diff = CompareJsons(oldMergedJsonObj.ToString(), newMergedJsonObj.ToString());
        
        Log.Information("Generate the compare result: {@Diff}", diff);

        try
        {
            var brief = await GenerateKnowledgeChangeBriefAsync(diff.ToString(), cancellationToken).ConfigureAwait(false);
        
            Log.Information($"Generate the knowledge chang brief: {brief}");

            if (!string.IsNullOrEmpty(brief))
            {
                var knowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(
                    @event.LatestKnowledge.AssistantId, @event.LatestKnowledge.Id, cancellationToken: cancellationToken).ConfigureAwait(false);

                knowledge.Brief = brief;
                await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantKnowledgesAsync([knowledge], cancellationToken: cancellationToken).ConfigureAwait(false);
                
                Log.Information( "knowledgeIdToSync Id: {@PrevKnowledge} , {@knowledgeIdToSync}", @event.PrevKnowledge.Id, knowledge.Id);

                var targerPrevRelateds = await _aiSpeechAssistantDataProvider.GetKnowledgeCopyRelatedBySourceKnowledgeIdAsync([@event.PrevKnowledge.Id], cancellationToken).ConfigureAwait(false);
                Log.Information("targerPrevRelateds prev relateds: {@allPrevRelatedIds}", targerPrevRelateds.Select(r => r.Id).ToList());
               
                var checkShouldSyncRelation = @event.ShouldSyncLastedKnowledge && targerPrevRelateds.Any();
                
                if (checkShouldSyncRelation)
                {
                    _smartTalkBackgroundJobClient.Enqueue<IAiSpeechAssistantService>(x => x.SyncCopiedKnowledgesIfRequiredAsync(
                        @event.PrevKnowledge.Id, @event.ShouldSyncLastedKnowledge,  false, CancellationToken.None));
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("Generate the knowledge brief error: {@Message}", e.Message);
        }
    }
    
    private JObject BuildMergedKnowledgeJson(string baseJson, IEnumerable<string> copyKnowledgePoints)
    {
        var baseObj = JObject.Parse(baseJson ?? "{}");
        
        var copyObjs = (copyKnowledgePoints ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(JObject.Parse); 
        
        return new[] { baseObj }
            .Concat(copyObjs)
            .Aggregate(new JObject(), (acc, j) =>
            {
                acc.Merge(j, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Concat });
                return acc;
            });
    }

    private JObject CompareJsons(string oldJson, string newJson)
    {
        var result = new JObject();;
        var oldObj = JObject.Parse(oldJson);
        var newObj = JObject.Parse(newJson);

        foreach (var property in oldObj.Properties())
        {
            var key = property.Name;
            var oldValue = property.Value;
            var newValue = newObj.TryGetValue(key, out var value) ? value : null;

            if (!JToken.DeepEquals(oldValue, newValue))
            {
                if (oldValue is JArray oldArray && newValue is JArray newArray)
                {
                    var arrayDiff = CompareJArrays(oldArray, newArray);
                    
                    if (arrayDiff.Count > 0) result[key] = arrayDiff;
                }
                else
                {
                    result[key] = new JArray
                    {
                        new JObject
                        {
                            ["old"] = oldValue,
                            ["new"] = newValue
                        }
                    };
                }
            }
        }
        
        foreach (var property in newObj.Properties())
        {
            var key = property.Name;
            if (!oldObj.ContainsKey(key))
            {
                result[key] = new JArray
                {
                    new JObject
                    {
                        ["old"] = null,
                        ["new"] = property.Value
                    }
                };
            }
        }

        return result;
    }

    private JArray CompareJArrays(JArray oldArray, JArray newArray)
    {
        var diff = new JArray();
        var maxLength = Math.Max(oldArray.Count, newArray.Count);

        for (var i = 0; i < maxLength; i++)
        {
            var oldValue = i < oldArray.Count ? oldArray[i] : null;
            var newValue = i < newArray.Count ? newArray[i] : null;

            if (!JToken.DeepEquals(oldValue, newValue))
            {
                diff.Add(new JObject
                {
                    ["old"] = oldValue,
                    ["new"] = newValue
                });
            }
        }

        return diff;
    }

    private async Task<string> GenerateKnowledgeChangeBriefAsync(string query, CancellationToken cancellationToken)
    {
        var completionResult = await _smartiesClient.PerformQueryAsync(new AskGptRequest
        {
            Messages = new List<CompletionsRequestMessageDto>
            {
                new () {
                    Role = "system",
                    Content = new CompletionsStringContent("你是一個善於分析數據的助手，專門用於對內容變更進行簡要概括。請根據提供的變更內容，生成不超过 10 字的簡短總結，只需點明變更重點，無需過多解釋。")
                },
                new ()
                {
                    Role = "user",
                    Content = new CompletionsStringContent($"input: {query}, output:")
                }
            },
            Model = OpenAiModel.Gpt4oMini
        }, cancellationToken).ConfigureAwait(false);
        
        return completionResult?.Data?.Response;
    }

    public async Task HandlingEventAsync(AiSpeechAssistantKonwledgeCopyAddedEvent @event, CancellationToken cancellationToken)
    {
        if (@event.KnowledgeOldJsons == null || @event.KnowledgeOldJsons.Count == 0) return;

        Log.Information("KonwledgeCopyAddedEvent KnowledgeId: {@Diff}", @event.KnowledgeOldJsons.Select(x=>x.KnowledgeId).ToList());
        
        try
        {
            var knowledgeIds = @event.KnowledgeOldJsons.Select(s => s.KnowledgeId).ToList();
            var knowledges = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgesAsync(knowledgeIds, cancellationToken).ConfigureAwait(false);

            var updates = new List<AiSpeechAssistantKnowledge>();

            foreach (var state in @event.KnowledgeOldJsons)
            {
                var knowledge = knowledges.FirstOrDefault(knowledge => knowledge.Id == state.KnowledgeId);
                if (knowledge == null) continue;

                var diff = CompareJsons(state.OldMergedJson, MergeJsons(new[] { JObject.Parse(state.OldMergedJson), JObject.Parse(@event.CopyJson) }));

                if (diff == null || !diff.HasValues) continue;

                var brief = await GenerateKnowledgeChangeBriefAsync(diff.ToString(), cancellationToken).ConfigureAwait(false);

                Log.Information($"KonwledgeCopyAddedEvent Generate the knowledge chang brief: {brief}");
                
                if (!string.IsNullOrEmpty(brief))
                {
                    knowledge.Brief = brief;
                    updates.Add(knowledge);
                }
            }

            if (updates.Count > 0)
            {
                await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantKnowledgesAsync(updates, true, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "KonwledgeCopyAddedEvent Generate knowledge brief error for multiple copy targets");
        }
    }
    
    private static string MergeJsons(IEnumerable<JObject> jsons)
    {
        return jsons.Aggregate(new JObject(), (acc, j) =>
        { acc.Merge(j, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Concat }); return acc; }).ToString(Formatting.None);
    }
}