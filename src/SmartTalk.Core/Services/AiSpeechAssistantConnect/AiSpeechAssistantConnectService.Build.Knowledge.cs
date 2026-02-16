using Serilog;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Smarties;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private async Task BuildKnowledgeAsync(CancellationToken cancellationToken)
    {
        var (assistant, knowledge, userProfile) = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantInfoByNumbersAsync(
                _ctx.From, _ctx.To, _ctx.ForwardAssistantId ?? _ctx.AssistantId, cancellationToken).ConfigureAwait(false);

        Log.Information("[AiAssistant] Assistant matched, AssistantId: {AssistantId}, HasProfile: {HasProfile}, From: {From}, To: {To}",
            assistant?.Id, userProfile != null, _ctx.From, _ctx.To);

        _ctx.Assistant = _mapper.Map<AiSpeechAssistantDto>(assistant);
        _ctx.Assistant.ModelName = assistant.ModelName;
        _ctx.Knowledge = _mapper.Map<AiSpeechAssistantKnowledgeDto>(knowledge);

        _ctx.Prompt = await ResolvePromptVariablesAsync(
            assistant.Name, userProfile?.ProfileJson, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> ResolvePromptVariablesAsync(
        string assistantName, string userProfileJson, CancellationToken cancellationToken)
    {
        var prompt = _ctx.Knowledge.Prompt;
        var pstTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));

        prompt = prompt
            .Replace("#{user_profile}", string.IsNullOrEmpty(userProfileJson) ? " " : userProfileJson)
            .Replace("#{current_time}", pstTime.ToString("yyyy-MM-dd HH:mm:ss"))
            .Replace("#{customer_phone}", _ctx.From.StartsWith("+1") ? _ctx.From[2..] : _ctx.From)
            .Replace("#{pst_date}", $"{pstTime.Date:yyyy-MM-dd} {pstTime.DayOfWeek}");

        if (_ctx.NumberId.HasValue && prompt.Contains("#{greeting}"))
        {
            var greeting = await _smartiesClient
                .GetSaleAutoCallNumberAsync(new GetSaleAutoCallNumberRequest { Id = _ctx.NumberId.Value }, cancellationToken).ConfigureAwait(false);
            prompt = prompt.Replace("#{greeting}", !string.IsNullOrEmpty(greeting.Data.Number.Greeting) ? greeting.Data.Number.Greeting : string.Empty);
        }

        if (prompt.Contains("#{customer_items}", StringComparison.OrdinalIgnoreCase))
        {
            var soldToIds = !string.IsNullOrEmpty(assistantName) ? assistantName.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList() : [];

            if (soldToIds.Count > 0)
            {
                var caches = await _salesDataProvider.GetCustomerItemsCacheBySoldToIdsAsync(soldToIds, cancellationToken).ConfigureAwait(false);
                var items = caches.Where(c => !string.IsNullOrEmpty(c.CacheValue)).Select(c => c.CacheValue.Trim()).Distinct().Take(50);
                prompt = prompt.Replace("#{customer_items}", string.Join(Environment.NewLine + Environment.NewLine, items));
            }
        }

        if (prompt.Contains("#{menu_items}", StringComparison.OrdinalIgnoreCase))
        {
            var menuItems = await GenerateMenuItemsAsync(cancellationToken).ConfigureAwait(false);
            prompt = prompt.Replace("#{menu_items}", menuItems ?? "");
        }

        if (prompt.Contains("#{customer_info}", StringComparison.OrdinalIgnoreCase))
        {
            var cache = await _salesDataProvider.GetCustomerInfoCacheByPhoneNumberAsync(_ctx.From, cancellationToken).ConfigureAwait(false);
            prompt = prompt.Replace("#{customer_info}", cache?.CacheValue?.Trim() ?? " ");
        }

        return prompt;
    }
}
