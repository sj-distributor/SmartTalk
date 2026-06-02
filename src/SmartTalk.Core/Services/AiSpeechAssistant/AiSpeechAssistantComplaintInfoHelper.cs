using System.Text;
using SmartTalk.Core.Constants;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public static class AiSpeechAssistantComplaintInfoHelper
{
    private const string PromptInstructionMarker = "[SmartTalk complaint-info collection]";

    public static string AppendPromptInstructionIfEnabled(string prompt, IEnumerable<string> toolNames)
    {
        if (!HasCollectComplaintInfoTool(toolNames)) return prompt;

        prompt ??= string.Empty;
        if (prompt.Contains(PromptInstructionMarker, StringComparison.OrdinalIgnoreCase)) return prompt;

        return $"{prompt}{Environment.NewLine}{Environment.NewLine}" +
               $"{PromptInstructionMarker}{Environment.NewLine}" +
               "For goods accident, return, missing goods, damaged goods, wrong item, quality issue, or other complaint scenarios, keep using the knowledge-base wording. " +
               $"After each customer answer, call the `{OpenAiToolConstants.CollectComplaintInfo}` tool with the complaint fields already mentioned by the customer. " +
               "The tool will return complaint placeholders such as #{complaint_invoice_no}, #{complaint_products}, #{complaint_problem}, #{complaint_quantity}, #{complaint_delivery_date}, #{complaint_missing_fields}, and #{complaint_summary}. " +
               "Ask only for the missing fields shown by #{complaint_missing_fields}. When the required fields are complete, confirm #{complaint_summary} with the customer.";
    }

    public static AiSpeechAssistantComplaintInfoDto Merge(
        AiSpeechAssistantComplaintInfoDto current,
        AiSpeechAssistantComplaintInfoDto incoming)
    {
        current ??= new AiSpeechAssistantComplaintInfoDto();
        if (incoming == null) return current;

        current.InvoiceNumbers = MergeList(current.InvoiceNumbers, incoming.InvoiceNumbers);
        current.Products = MergeList(current.Products, incoming.Products);
        current.ProblemDescription = PickLatest(current.ProblemDescription, incoming.ProblemDescription);
        current.AffectedQuantity = PickLatest(current.AffectedQuantity, incoming.AffectedQuantity);
        current.DeliveryDate = PickLatest(current.DeliveryDate, incoming.DeliveryDate);
        current.DeliveryDateText = PickLatest(current.DeliveryDateText, incoming.DeliveryDateText);
        current.IsConfirmed = incoming.IsConfirmed ?? current.IsConfirmed;

        return current;
    }

    public static string BuildFunctionOutput(AiSpeechAssistantComplaintInfoDto info)
    {
        info ??= new AiSpeechAssistantComplaintInfoDto();

        var builder = new StringBuilder();
        builder.AppendLine("Complaint information has been recorded. Continue the conversation using the assistant's knowledge-base script, not a hard-coded script.");
        builder.AppendLine("If the knowledge-base script contains complaint placeholders, fill them with the current values below:");
        builder.AppendLine($"#{{complaint_invoice_no}}={Join(info.InvoiceNumbers)}");
        builder.AppendLine($"#{{complaint_products}}={Join(info.Products)}");
        builder.AppendLine($"#{{complaint_problem}}={ValueOrUnknown(info.ProblemDescription)}");
        builder.AppendLine($"#{{complaint_quantity}}={ValueOrUnknown(info.AffectedQuantity)}");
        builder.AppendLine($"#{{complaint_delivery_date}}={ValueOrUnknown(FirstNonEmpty(info.DeliveryDateText, info.DeliveryDate))}");
        builder.AppendLine($"#{{complaint_missing_fields}}={BuildMissingFieldsText(info)}");
        builder.AppendLine($"#{{complaint_summary}}={BuildSummary(info)}");
        builder.AppendLine("If complaint_missing_fields is not empty, ask only for the missing fields. If all required fields are present, ask the customer to confirm complaint_summary.");

        return builder.ToString().Trim();
    }

    private static List<string> MergeList(IEnumerable<string> current, IEnumerable<string> incoming)
    {
        return (current ?? [])
            .Concat(incoming ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool HasCollectComplaintInfoTool(IEnumerable<string> toolNames)
    {
        return toolNames?
            .Any(x => string.Equals(x, OpenAiToolConstants.CollectComplaintInfo, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static string PickLatest(string current, string incoming)
    {
        return string.IsNullOrWhiteSpace(incoming) ? current : incoming.Trim();
    }

    private static string Join(IEnumerable<string> values)
    {
        var list = (values ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return list.Count == 0 ? "未识别" : string.Join("、", list);
    }

    private static string ValueOrUnknown(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "未识别" : value.Trim();
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
    }

    private static string BuildMissingFieldsText(AiSpeechAssistantComplaintInfoDto info)
    {
        var missingFields = new List<string>();

        if (info.InvoiceNumbers == null || info.InvoiceNumbers.All(string.IsNullOrWhiteSpace))
            missingFields.Add("Invoice单号");

        if (info.Products == null || info.Products.All(string.IsNullOrWhiteSpace))
            missingFields.Add("投诉商品");

        if (string.IsNullOrWhiteSpace(info.ProblemDescription))
            missingFields.Add("问题描述");

        if (string.IsNullOrWhiteSpace(info.AffectedQuantity))
            missingFields.Add("受影响数量");

        if (string.IsNullOrWhiteSpace(info.DeliveryDate) && string.IsNullOrWhiteSpace(info.DeliveryDateText))
            missingFields.Add("送货日期");

        return missingFields.Count == 0 ? "无" : string.Join("、", missingFields);
    }

    private static string BuildSummary(AiSpeechAssistantComplaintInfoDto info)
    {
        return $"发票{Join(info.InvoiceNumbers)}，商品{Join(info.Products)}，问题{ValueOrUnknown(info.ProblemDescription)}，数量{ValueOrUnknown(info.AffectedQuantity)}，送货日期{ValueOrUnknown(FirstNonEmpty(info.DeliveryDateText, info.DeliveryDate))}";
    }
}
