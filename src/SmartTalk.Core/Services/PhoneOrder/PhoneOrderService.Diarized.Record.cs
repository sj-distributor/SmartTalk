using OpenAI.Chat;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.STT;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderService
{
    Task ProcessPhoneOrderDiarizedTranscriptionAsync(List<PhoneOrderDiarizedSpeakInfoDto> phoneOrderInfo, PhoneOrderRecord record, CancellationToken cancellationToken);
}

public partial class PhoneOrderService
{
    public Task<(string GoalText, string Tip, List<PhoneOrderConversation> Conversations)> PhoneOrderDiarizedTranscriptionAsync(List<PhoneOrderDiarizedSpeakInfoDto> phoneOrderInfo, PhoneOrderRecord record, CancellationToken cancellationToken)
    {
        if (phoneOrderInfo is not { Count: > 0 })
            return Task.FromResult<(string GoalText, string Tip, List<PhoneOrderConversation> Conversations)>((string.Empty, string.Empty, []));

        Log.Information("Diarized phone order transcription input: {@PhoneOrderInfo}", phoneOrderInfo);

        return PhoneOrderDiarizedTranscriptionInternalAsync(phoneOrderInfo, record);
    }

    private Task<(string GoalText, string Tip, List<PhoneOrderConversation> Conversations)> PhoneOrderDiarizedTranscriptionInternalAsync(List<PhoneOrderDiarizedSpeakInfoDto> phoneOrderInfo, PhoneOrderRecord record)
    {
        var conversationIndex = 0;
        var goalTexts = new List<string>();
        var conversations = new List<PhoneOrderConversation>();
        PhoneOrderRole? previousRole = null;

        foreach (var speakDetail in phoneOrderInfo)
        {
            Log.Information(
                "Diarized segment. Start: {StartTime}, End: {EndTime}, Speaker: {Speaker}, Role: {Role}, RoleText: {RoleText}",
                speakDetail.StartTime,
                speakDetail.EndTime,
                speakDetail.Speaker,
                speakDetail.Role,
                speakDetail.RoleText);

            try
            {
                var originText = speakDetail.Text ?? string.Empty;
                var isRestaurantTurn = string.Equals(speakDetail.Speaker, "S1", StringComparison.OrdinalIgnoreCase);
                var currentRole = isRestaurantTurn ? PhoneOrderRole.Restaurant : PhoneOrderRole.Client;

                Log.Information("Diarized transcription originText: {OriginText}", originText);

                goalTexts.Add((currentRole == PhoneOrderRole.Restaurant
                    ? PhoneOrderRole.Restaurant.ToString()
                    : PhoneOrderRole.Client.ToString()) + ": " + originText);

                if (currentRole == PhoneOrderRole.Restaurant)
                {
                    if (previousRole == PhoneOrderRole.Restaurant && conversations.Count > 0 && string.IsNullOrWhiteSpace(conversations[^1].Answer))
                    {
                        conversations[^1].Question = JoinDiarizedConversationText(conversations[^1].Question, originText);
                        conversations[^1].EndTime = MaxDiarizedConversationEndTime(conversations[^1].EndTime, speakDetail.EndTime);
                    }
                    else
                    {
                        conversations.Add(new PhoneOrderConversation
                        {
                            RecordId = record.Id,
                            Question = originText,
                            Answer = string.Empty,
                            Order = conversationIndex,
                            StartTime = speakDetail.StartTime,
                            EndTime = speakDetail.EndTime,
                            CreatedDate = DateTimeOffset.Now
                        });
                    }
                }
                else
                {
                    if (previousRole == PhoneOrderRole.Client && conversations.Count > 0)
                    {
                        conversations[^1].Answer = JoinDiarizedConversationText(conversations[^1].Answer, originText);
                        conversations[^1].EndTime = MaxDiarizedConversationEndTime(conversations[^1].EndTime, speakDetail.EndTime);
                    }
                    else
                    {
                        if (conversationIndex >= conversations.Count)
                        {
                            conversations.Add(new PhoneOrderConversation
                            {
                                RecordId = record.Id,
                                Question = string.Empty,
                                Answer = string.Empty,
                                Order = conversationIndex,
                                StartTime = speakDetail.StartTime,
                                EndTime = speakDetail.EndTime,
                                CreatedDate = DateTimeOffset.Now
                            });
                        }

                        conversations[conversationIndex].Answer = originText;
                        conversations[conversationIndex].EndTime = MaxDiarizedConversationEndTime(conversations[conversationIndex].EndTime, speakDetail.EndTime);
                        conversationIndex++;
                    }
                }

                previousRole = currentRole;
            }
            catch (Exception ex)
            {
                Log.Information("Diarized phone order transcription error: {ErrorMessage}", ex.Message);
            }
        }

        var goalTextsString = ProcessDiarizedConversation(conversations, string.Join("\n", goalTexts));

        return Task.FromResult((goalTextsString, conversations.FirstOrDefault()?.Question ?? goalTextsString, conversations));
    }

    private async Task PersistPhoneOrderDiarizedTranscriptionAsync(PhoneOrderRecord record, string goalText, string transcript, List<PhoneOrderConversation> conversations, CancellationToken cancellationToken)
    {
        var persistableConversations = conversations.Count != 0
            ? conversations
            :
            [
                new PhoneOrderConversation
                {
                    RecordId = record.Id,
                    Question = !string.IsNullOrWhiteSpace(goalText)
                        ? goalText
                        : (!string.IsNullOrWhiteSpace(record.TranscriptionText) ? record.TranscriptionText : transcript),
                    Answer = string.Empty,
                    Order = 0,
                    CreatedDate = DateTimeOffset.Now
                }
            ];

        await _phoneOrderDataProvider.DeletePhoneOrderConversationsAsync(record.Id, cancellationToken).ConfigureAwait(false);
        await _phoneOrderDataProvider.AddPhoneOrderConversationsAsync(persistableConversations, true, cancellationToken).ConfigureAwait(false);

        record.Tips = persistableConversations.FirstOrDefault()?.Question ?? string.Empty;
        record.ConversationText = string.Join("\n", persistableConversations.Select(x =>
            $"Restaurant: {x.Question ?? string.Empty}\nClient:{x.Answer ?? string.Empty}"));
    }

    public async Task ProcessPhoneOrderDiarizedTranscriptionAsync(List<PhoneOrderDiarizedSpeakInfoDto> phoneOrderInfo, PhoneOrderRecord record, CancellationToken cancellationToken)
    {
        try
        {
            var transcript = string.Join("\n", phoneOrderInfo.Where(x => !string.IsNullOrWhiteSpace(x.Text)).Select(x => $"{x.Speaker}: {x.Text}"));

            var (goalText, _, conversations) = phoneOrderInfo.Count == 0
                ? (string.Empty, string.Empty, new List<PhoneOrderConversation>())
                : await PhoneOrderDiarizedTranscriptionAsync(phoneOrderInfo, record, cancellationToken).ConfigureAwait(false);

            var originalReport = await _phoneOrderDataProvider.GetOriginalPhoneOrderRecordReportAsync(record.Id, cancellationToken).ConfigureAwait(false);
            var reportText = originalReport?.Report ?? record.TranscriptionText ?? string.Empty;

            var optimizedConversations = conversations.Count == 0
                ? conversations
                : await OptimizePhoneOrderDiarizedConversationsAsync(
                    phoneOrderInfo,
                    conversations,
                    reportText,
                    cancellationToken).ConfigureAwait(false);

            await PersistPhoneOrderDiarizedTranscriptionAsync(record, goalText, transcript, optimizedConversations, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Error("Process phone order diarized transcription error: {@Error}", e);
        }
    }

    private async Task<List<PhoneOrderConversation>> OptimizePhoneOrderDiarizedConversationsAsync(List<PhoneOrderDiarizedSpeakInfoDto> speakInfos, List<PhoneOrderConversation> draftConversations, string reportText, CancellationToken cancellationToken)
    {
        if (draftConversations is not { Count: > 0 })
            return draftConversations;

        var client = new ChatClient("gpt-5.1", _openAiSettings.ApiKey);

        var draftPayload = draftConversations
            .OrderBy(x => x.Order)
            .Select(x => new
            {
                x.Order,
                x.Question,
                x.Answer
            }).ToList();

        var diarizedPayload = speakInfos
            .Select(x => new
            {
                startTime = x.StartTime,
                endTime = x.EndTime,
                speaker = x.Speaker,
                role = x.Role?.ToString(),
                text = x.Text
            }).ToList();

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("""
                                  You are a transcript cleanup assistant for wholesale order phone calls.
                                  You will receive:
                                  1. diarized transcription turns
                                  2. draft conversations where question=agent/customer service and answer=user/customer

                                  Your task is to clean up only the text content while preserving the existing conversation order.

                                  Rules:
                                  1. Return a JSON object with one field: conversations.
                                  2. Keep the same number of conversation items and keep the same order values.
                                  3. Only rewrite question and answer text. Do not add new conversations. Do not remove conversations.
                                  4. Preserve speaker ownership: question is the service side, answer is the user side.
                                  5. You will also receive an original report generated from the whole call. Treat that report as a correction reference for product names, quantities, units, and obvious intent.
                                  6. Use the report to correct text only when the report clearly supports the correction.
                                  7. Fix obvious ASR mistakes using the full context of the call.
                                  8. Fix incorrect Chinese quantity/unit words when the intended wording is clear from context.
                                  9. If Chinese was transcribed as pinyin, rewrite it into proper Chinese characters.
                                  10. If a product name is clearly inconsistent, duplicated incorrectly, or does not make sense in context, rewrite it to the most plausible product mentioned elsewhere in the transcript or report.
                                  11. Do not translate fluent English into Chinese. Only normalize obvious pinyin-Chinese or transcription errors.
                                  12. When the opening greeting is the standard service-side phrase like "你好，这里是OME，请问有什么可以帮到你？", normalize the brand name to "OME". Variants like "omi", "lme" and other similar mistakes should be corrected to "OME" when the context clearly indicates the same greeting.
                                  13. Preserve all spoken content. Do not compress, summarize, merge away, or simplify repeated wording. If a sentence repeats part of itself, keep the repetition in the cleaned transcript.
                                  14. Do not merge adjacent repeated clauses.
                                  15. Do not delete spoken repetition or filler repetition if it was actually said.
                                  16. Do not compress two similar or near-duplicate spoken sentences into one sentence.
                                  17. Do not invent, infer, or supplement customer utterances that are not actually supported by the transcript or report. The principle is: do not miss content, and do not add content.
                                  18. Do not invent new products, quantities, prices, addresses, or requests that are not supported by the transcript or report.
                                  19. If something is uncertain, keep the original wording instead of guessing.
                                  20. Return valid JSON only.

                                  Example corrections:
                                  - "一香鸡腿肉" can become "一箱鸡胸肉" if the full call context clearly supports it.
                                  - "Bao qian, wo zhe bian mei you ji xiong rou de chi cun zi xun." should become "抱歉，我这边没有鸡胸肉的尺寸资讯。"
                                  - If the transcript contains both "流通果" and "牛筒骨" and context shows they refer to the same requested item, normalize them to "牛筒骨".
                                  - "你好，这里是omi，请问有什么可以帮到你？" should become "你好，这里是OME，请问有什么可以帮到你？" when it is clearly the standard opening greeting.
                                  - "最近厨房说我们餐厅的工作量有点大，最近厨房说虾处理起来太花时间了。" must keep both clauses fully, and must not be simplified into a shorter summary.
                                  - Do not add extra customer wording that was not actually spoken, even if you think it would make the dialogue sound more complete.
                                  """),
            new UserChatMessage(
                "Diarized turns JSON:\n" +
                JsonConvert.SerializeObject(diarizedPayload, Formatting.None) +
                "\n\nDraft conversations JSON:\n" +
                JsonConvert.SerializeObject(draftPayload, Formatting.None) +
                "\n\nOriginal report text:\n" +
                reportText)
        };

        var completion = await client.CompleteChatAsync(messages, new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        }, cancellationToken).ConfigureAwait(false);

        var jsonResponse = completion.Value.Content.FirstOrDefault()?.Text;

        if (string.IsNullOrWhiteSpace(jsonResponse))
        {
            Log.Warning("Diarized phone order conversation cleanup returned empty response. Falling back to draft conversations.");
            return draftConversations;
        }

        DiarizedConversationCleanupResponse cleanupResponse;

        try
        {
            cleanupResponse = JsonConvert.DeserializeObject<DiarizedConversationCleanupResponse>(jsonResponse);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to deserialize diarized phone order conversation cleanup response. Falling back to draft conversations. Response: {Response}", jsonResponse);
            return draftConversations;
        }

        if (cleanupResponse?.Conversations is not { Count: > 0 })
        {
            Log.Warning("Diarized phone order conversation cleanup returned no conversations. Falling back to draft conversations. Response: {Response}", jsonResponse);
            return draftConversations;
        }

        var optimizedConversationMap = cleanupResponse.Conversations
            .GroupBy(x => x.Order)
            .ToDictionary(
                x => x.Key,
                x => x.First());

        var optimizedConversations = draftConversations
            .OrderBy(x => x.Order)
            .Select(conversation =>
            {
                if (!optimizedConversationMap.TryGetValue(conversation.Order, out var optimized))
                    return conversation;

                conversation.Question = string.IsNullOrWhiteSpace(optimized.Question)
                    ? string.Empty
                    : optimized.Question.Trim();
                conversation.Answer = string.IsNullOrWhiteSpace(optimized.Answer)
                    ? string.Empty
                    : optimized.Answer.Trim();

                return conversation;
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Question) || !string.IsNullOrWhiteSpace(x.Answer))
            .ToList();

        Log.Information("Optimized diarized phone order conversations: {@Conversations}", optimizedConversations);

        return optimizedConversations.Count == 0 ? draftConversations : optimizedConversations;
    }

    private static string ProcessDiarizedConversation(List<PhoneOrderConversation> conversations, string goalTextsString)
    {
        if (conversations == null || conversations.Count == 0) return goalTextsString;

        goalTextsString = string.Empty;

        foreach (var conversation in conversations.ToList())
        {
            if (string.IsNullOrEmpty(conversation.Answer) && string.IsNullOrEmpty(conversation.Question))
            {
                conversations.Remove(conversation);
                continue;
            }

            conversation.Answer ??= string.Empty;
            conversation.Question ??= string.Empty;

            goalTextsString = goalTextsString + "Restaurant: " + conversation.Question + "\nClient:" + conversation.Answer + "\n";
        }

        Log.Information("Processed diarized phone order conversation: {@Conversations}, GoalText: {GoalText}", conversations, goalTextsString);

        return goalTextsString;
    }

    private static string JoinDiarizedConversationText(string previousText, string currentText)
    {
        if (string.IsNullOrWhiteSpace(previousText))
            return currentText?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(currentText))
            return previousText.Trim();

        var left = previousText.Trim();
        var right = currentText.Trim();

        return right.StartsWith(",", StringComparison.Ordinal) || right.StartsWith(".", StringComparison.Ordinal)
            ? left + right
            : left + " " + right;
    }

    private static double MaxDiarizedConversationEndTime(double? previousEndTime, double currentEndTime)
    {
        return previousEndTime.HasValue && previousEndTime.Value > currentEndTime
            ? previousEndTime.Value
            : currentEndTime;
    }

    private sealed class DiarizedConversationCleanupResponse
    {
        public List<DiarizedConversationCleanupItem> Conversations { get; set; } = [];
    }

    private sealed class DiarizedConversationCleanupItem
    {
        public int Order { get; set; }

        public string Question { get; set; }

        public string Answer { get; set; }
    }
}
