using Serilog;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderService
{
    Task<(string GoalText, string Tip, List<PhoneOrderConversation> Conversations)> BuildOpenAiComparisonConversationsAsync(List<PhoneOrderOpenAiSpeakInfoDto> phoneOrderInfo, PhoneOrderRecord record, CancellationToken cancellationToken);
}

public partial class PhoneOrderService
{
    public Task<(string GoalText, string Tip, List<PhoneOrderConversation> Conversations)> BuildOpenAiComparisonConversationsAsync(List<PhoneOrderOpenAiSpeakInfoDto> phoneOrderInfo, PhoneOrderRecord record, CancellationToken cancellationToken)
    {
        if (phoneOrderInfo is not { Count: > 0 })
            return Task.FromResult<(string GoalText, string Tip, List<PhoneOrderConversation> Conversations)>((string.Empty, string.Empty, []));

        Log.Information("OpenAI comparison phone order info: {@PhoneOrderInfo}", phoneOrderInfo);

        return OpenAiComparisonTranscriptionAsync(phoneOrderInfo, record);
    }

    private Task<(string GoalText, string Tip, List<PhoneOrderConversation> Conversations)> OpenAiComparisonTranscriptionAsync(List<PhoneOrderOpenAiSpeakInfoDto> phoneOrderInfo, PhoneOrderRecord record)
    {
        var conversationIndex = 0;
        var goalTexts = new List<string>();
        var conversations = new List<PhoneOrderConversation>();
        PhoneOrderRole? previousRole = null;

        foreach (var speakDetail in phoneOrderInfo)
        {
            Log.Information(
                "OpenAI comparison segment. Start: {StartTime}, End: {EndTime}, Speaker: {Speaker}, Role: {Role}, RoleText: {RoleText}",
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

                Log.Information("OpenAI comparison originText: {OriginText}", originText);

                goalTexts.Add((currentRole == PhoneOrderRole.Restaurant
                    ? PhoneOrderRole.Restaurant.ToString()
                    : PhoneOrderRole.Client.ToString()) + ": " + originText);

                if (currentRole == PhoneOrderRole.Restaurant)
                {
                    if (previousRole == PhoneOrderRole.Restaurant && conversations.Count > 0 && string.IsNullOrWhiteSpace(conversations[^1].Answer))
                    {
                        conversations[^1].Question = JoinOpenAiConversationText(conversations[^1].Question, originText);
                        conversations[^1].EndTime = MaxOpenAiConversationEndTime(conversations[^1].EndTime, speakDetail.EndTime);
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
                        conversations[^1].Answer = JoinOpenAiConversationText(conversations[^1].Answer, originText);
                        conversations[^1].EndTime = MaxOpenAiConversationEndTime(conversations[^1].EndTime, speakDetail.EndTime);
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
                        conversations[conversationIndex].EndTime = MaxOpenAiConversationEndTime(conversations[conversationIndex].EndTime, speakDetail.EndTime);
                        conversationIndex++;
                    }
                }

                previousRole = currentRole;
            }
            catch (Exception ex)
            {
                Log.Information("OpenAI comparison transcription error: {ErrorMessage}", ex.Message);
            }
        }

        var goalTextsString = ProcessOpenAiComparisonConversation(conversations, string.Join("\n", goalTexts));

        return Task.FromResult((goalTextsString, conversations.FirstOrDefault()?.Question ?? goalTextsString, conversations));
    }

    private static string ProcessOpenAiComparisonConversation(List<PhoneOrderConversation> conversations, string goalTextsString)
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

        Log.Information("Processed OpenAI comparison conversation: {@Conversations}, GoalText: {GoalText}", conversations, goalTextsString);

        return goalTextsString;
    }

    private static string JoinOpenAiConversationText(string previousText, string currentText)
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

    private static double MaxOpenAiConversationEndTime(double? previousEndTime, double currentEndTime)
    {
        return previousEndTime.HasValue && previousEndTime.Value > currentEndTime
            ? previousEndTime.Value
            : currentEndTime;
    }
}
