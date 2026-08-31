using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    public static string AppendCustomerItemsToolInstructions(
        string prompt,
        IEnumerable<AiSpeechAssistantFunctionCall> functionCalls)
    {
        return CustomerItemsToolPrompt.AppendInstructions(prompt, functionCalls);
    }

    private static class CustomerItemsToolPrompt
    {
        private const string Header = "Realtime tool rule for query_customer_items_by_store_name:";
        private const string Description =
            "Confirm a store name in a multi-store call and load that store's cached HiFood customer items into the current session knowledge.";

        private const string Instructions =
            Header + "\n" +
            "- When the guest merely mentions or corrects a store, restaurant, or shop name, immediately call query_customer_items_by_store_name with store_name set to the name exactly as heard and prefetch_only set to true. This silently replaces the customer-items knowledge placeholder for later in the current call; do not give a spoken response after that tool call.\n" +
            "- If the guest asks a product, stock, availability, warehouse-goods, or orderable-goods question while providing or changing the store name in the same turn, call the tool with prefetch_only set to false so you can answer after it updates the knowledge.\n" +
            "- After a store has been confirmed, answer later product, stock, availability, warehouse-goods, or orderable-goods questions only from the current customer-items knowledge. Do not call this tool again unless the guest provides or corrects a different store name.\n" +
            "- If no store has been confirmed and the guest asks about HiFood product information, ask for the store, restaurant, or shop name first.\n" +
            "- Never use product, stock, availability, warehouse-goods, or orderable-goods information from memory or another store.";

        public static string AppendInstructions(
            string prompt,
            IEnumerable<AiSpeechAssistantFunctionCall> functionCalls)
        {
            if (!HasCustomerItemsTool(functionCalls)) return prompt;
            if (prompt?.Contains(Header, StringComparison.OrdinalIgnoreCase) == true) return prompt;

            return string.IsNullOrWhiteSpace(prompt)
                ? Instructions
                : prompt.TrimEnd() + "\n\n" + Instructions;
        }

        private static bool HasCustomerItemsTool(IEnumerable<AiSpeechAssistantFunctionCall> functionCalls)
        {
            return functionCalls?.Any(x =>
                x.Type == AiSpeechAssistantSessionConfigType.Tool &&
                x.Name == OpenAiToolConstants.QueryCustomerItemsByStoreName &&
                x.IsActive) == true;
        }

        public static object DeserializeSessionToolContent(AiSpeechAssistantFunctionCall functionCall)
        {
            var content = JsonConvert.DeserializeObject<object>(functionCall.Content);

            if (functionCall.Name != OpenAiToolConstants.QueryCustomerItemsByStoreName || content is not JObject tool)
                return content;

            tool["description"] = Description;

            var properties = tool["parameters"]?["properties"] as JObject;
            if (properties == null)
                return tool;

            properties.Remove("product_name");
            if (tool["parameters"]?["required"] is JArray required)
            {
                foreach (var token in required
                             .Where(x => string.Equals(x?.Value<string>(), "product_name", StringComparison.OrdinalIgnoreCase))
                             .ToList())
                {
                    token.Remove();
                }
            }

            if (properties["prefetch_only"] == null)
            {
                properties["prefetch_only"] = JObject.FromObject(new
                {
                    type = "boolean",
                    description = "Set true only when the guest merely provides or corrects the store name and is not asking a product, stock, availability, warehouse, or orderable-goods question. The matching customer item cache replaces the session knowledge placeholder silently for a later guest question."
                });
            }

            return tool;
        }
    }
}
