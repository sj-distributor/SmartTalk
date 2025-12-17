using Newtonsoft.Json.Linq;
using Serilog;
using Smarties.Messages.DTO.OpenAi;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Messages.Events.AiSpeechAssistant;

namespace SmartTalk.Core.Services.EventHandling;

public partial class EventHandlingService
{
    public async Task HandlingEventAsync(AiSpeechAssistantKnowledgeAddedEvent @event, CancellationToken cancellationToken)
    {
        var diff = CompareJsons(@event.PrevKnowledge.Json, @event.LatestKnowledge.Json);
        
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
                
                await _aiSpeechAssistantService.SyncCopiedKnowledgesIfRequiredAsync([knowledge], false, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception e)
        {
            Log.Error("Generate the knowledge brief error: {@Message}", e.Message);
        }
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
}