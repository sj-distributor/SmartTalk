using Serilog;
using SmartTalk.Messages.Dto.Smarties;
using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private async Task BuildKnowledgeAsync(CancellationToken cancellationToken)
    {
        await LoadAssistantInfoAsync(cancellationToken).ConfigureAwait(false);
        
        ResolveStaticPromptVariables();
        
        await ResolveGreetingAsync(cancellationToken).ConfigureAwait(false);
        await ResolveCustomerItemsAsync(cancellationToken).ConfigureAwait(false);
        await ResolveMenuItemsAsync(cancellationToken).ConfigureAwait(false);
        await ResolveCustomerInfoAsync(cancellationToken).ConfigureAwait(false);

        Log.Information("[AiAssistant] Prompt resolved, Prompt: {Prompt}", _ctx.Prompt);
    }

    private async Task LoadAssistantInfoAsync(CancellationToken cancellationToken)
    {
        var (assistant, knowledge, userProfile) = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantInfoByNumbersAsync(_ctx.From, _ctx.To, _ctx.ForwardAssistantId ?? _ctx.AssistantId, cancellationToken).ConfigureAwait(false);

        Log.Information("[AiAssistant] Assistant matched, AssistantId: {AssistantId}, HasProfile: {HasProfile}, From: {From}, To: {To}", assistant?.Id, userProfile != null, _ctx.From, _ctx.To);

        _ctx.Assistant = _mapper.Map<AiSpeechAssistantDto>(assistant);
        _ctx.Knowledge = _mapper.Map<AiSpeechAssistantKnowledgeDto>(knowledge);
        
        _ctx.Prompt = _ctx.Knowledge.Prompt;
        _ctx.UserProfileJson = userProfile?.ProfileJson;
    }

    private void ResolveStaticPromptVariables()
    {
        var pstTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));

        _ctx.Prompt = _ctx.Prompt
            .Replace("#{user_profile}", string.IsNullOrEmpty(_ctx.UserProfileJson) ? " " : _ctx.UserProfileJson)
            .Replace("#{current_time}", pstTime.ToString("yyyy-MM-dd HH:mm:ss"))
            .Replace("#{customer_phone}", _ctx.From.StartsWith("+1") ? _ctx.From[2..] : _ctx.From)
            .Replace("#{pst_date}", $"{pstTime.Date:yyyy-MM-dd} {pstTime.DayOfWeek}");
    }

    private async Task ResolveGreetingAsync(CancellationToken cancellationToken)
    {
        if (!_ctx.NumberId.HasValue || !_ctx.Prompt.Contains("#{greeting}")) return;

        var greeting = await _smartiesClient
            .GetSaleAutoCallNumberAsync(new GetSaleAutoCallNumberRequest { Id = _ctx.NumberId.Value }, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(greeting.Data.Number.Greeting))
            _ctx.Knowledge.Greetings = greeting.Data.Number.Greeting;
        
        _ctx.Prompt = _ctx.Prompt.Replace("#{greeting}", _ctx.Knowledge.Greetings ?? string.Empty);
    }

    private async Task ResolveCustomerItemsAsync(CancellationToken cancellationToken)
    {
        if (!_ctx.Prompt.Contains("#{customer_items}", StringComparison.OrdinalIgnoreCase)) return;

        var soldToIds = !string.IsNullOrEmpty(_ctx.Assistant.Name) ? _ctx.Assistant.Name.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList() : [];
        if (soldToIds.Count == 0) return;

        var caches = await _salesDataProvider.GetCustomerItemsCacheBySoldToIdsAsync(soldToIds, cancellationToken).ConfigureAwait(false);
        var items = caches.Where(c => !string.IsNullOrEmpty(c.CacheValue)).Select(c => c.CacheValue.Trim()).Distinct().Take(50);

        _ctx.Prompt = _ctx.Prompt.Replace("#{customer_items}", string.Join(Environment.NewLine + Environment.NewLine, items));
    }

    private async Task ResolveMenuItemsAsync(CancellationToken cancellationToken)
    {
        if (!_ctx.Prompt.Contains("#{menu_items}", StringComparison.OrdinalIgnoreCase)) return;

        var menuItems = await GenerateMenuItemsAsync(cancellationToken).ConfigureAwait(false);

        _ctx.Prompt = _ctx.Prompt.Replace("#{menu_items}", menuItems ?? "");
    }

    private async Task ResolveCustomerInfoAsync(CancellationToken cancellationToken)
    {
        if (!_ctx.Prompt.Contains("#{customer_info}", StringComparison.OrdinalIgnoreCase)) return;

        var cache = await _salesDataProvider.GetCustomerInfoCacheByPhoneNumberAsync(_ctx.From, cancellationToken).ConfigureAwait(false);

        _ctx.Prompt = _ctx.Prompt.Replace("#{customer_info}", cache?.CacheValue?.Trim() ?? " ");
    }
}
