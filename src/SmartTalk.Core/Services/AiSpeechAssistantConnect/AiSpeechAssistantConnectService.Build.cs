using Mediator.Net;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Dto.Smarties;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    #region Knowledge Base

    private static AiSpeechAssistantConnectContext BuildContext(ConnectAiSpeechAssistantCommand command) => new()
    {
        Host = command.Host,
        From = command.From,
        To = command.To,
        AssistantId = command.AssistantId,
        NumberId = command.NumberId,
        TwilioWebSocket = command.TwilioWebSocket,
        OrderRecordType = command.OrderRecordType,
        LastUserInfo = new AiSpeechAssistantUserInfoDto { PhoneNumber = command.From }
    };

    private async Task<RealtimeSessionOptions> BuildSessionConfigAsync(CancellationToken cancellationToken)
    {
        var resolvedPrompt = await BuildKnowledgeAsync(cancellationToken).ConfigureAwait(false);

        _ctx.HumanContactPhone = (await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantHumanContactByAssistantIdAsync(_ctx.Assistant.Id, cancellationToken)
            .ConfigureAwait(false))?.HumanPhone;

        var modelConfig = await BuildModelConfigAsync(resolvedPrompt, cancellationToken).ConfigureAwait(false);

        var timer = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantTimerByAssistantIdAsync(_ctx.Assistant.Id, cancellationToken).ConfigureAwait(false);

        return BuildSessionOptions(modelConfig, timer);
    }

    private async Task<(string forwardNumber, int? forwardAssistantId)> ResolveInboundRouteAsync(
        string from, string to, CancellationToken cancellationToken)
    {
        var inboundRoute = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantInboundRouteAsync(from, to, cancellationToken).ConfigureAwait(false);

        var (forwardNumber, forwardAssistantId) = DecideDestinationByInboundRoute(inboundRoute);

        Log.Information("[AiAssistant] Route resolved, Routes: {RouteCount}, ForwardNumber: {ForwardNumber}, ForwardAssistantId: {ForwardAssistantId}, From: {From}, To: {To}",
            inboundRoute?.Count ?? 0, forwardNumber, forwardAssistantId, from, to);

        return (forwardNumber, forwardAssistantId);
    }

    private async Task<string> BuildKnowledgeAsync(CancellationToken cancellationToken)
    {
        var (assistant, knowledge, userProfile) = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantInfoByNumbersAsync(
                _ctx.From, _ctx.To, _ctx.ForwardAssistantId ?? _ctx.AssistantId, cancellationToken).ConfigureAwait(false);

        Log.Information("[AiAssistant] Assistant matched, AssistantId: {AssistantId}, HasProfile: {HasProfile}, From: {From}, To: {To}",
            assistant?.Id, userProfile != null, _ctx.From, _ctx.To);

        var finalPrompt = await ResolvePromptVariablesAsync(
            knowledge.Prompt, _ctx.From, assistant, _ctx.NumberId, _ctx.AgentId, userProfile, cancellationToken).ConfigureAwait(false);

        _ctx.Assistant = _mapper.Map<AiSpeechAssistantDto>(assistant);
        _ctx.Assistant.ModelName = assistant.ModelName;
        _ctx.Knowledge = _mapper.Map<AiSpeechAssistantKnowledgeDto>(knowledge);

        return finalPrompt;
    }

    private async Task<string> ResolvePromptVariablesAsync(
        string prompt, string from,
        Core.Domain.AISpeechAssistant.AiSpeechAssistant assistant,
        int? numberId, int? agentId,
        Core.Domain.AIAssistant.AiSpeechAssistantUserProfile userProfile,
        CancellationToken cancellationToken)
    {
        var pstTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));

        prompt = prompt
            .Replace("#{user_profile}", string.IsNullOrEmpty(userProfile?.ProfileJson) ? " " : userProfile.ProfileJson)
            .Replace("#{current_time}", pstTime.ToString("yyyy-MM-dd HH:mm:ss"))
            .Replace("#{customer_phone}", from.StartsWith("+1") ? from[2..] : from)
            .Replace("#{pst_date}", $"{pstTime.Date:yyyy-MM-dd} {pstTime.DayOfWeek}");

        if (numberId.HasValue && prompt.Contains("#{greeting}"))
        {
            var greeting = await _smartiesClient
                .GetSaleAutoCallNumberAsync(new GetSaleAutoCallNumberRequest { Id = numberId.Value }, cancellationToken).ConfigureAwait(false);
            prompt = prompt.Replace("#{greeting}", !string.IsNullOrEmpty(greeting.Data.Number.Greeting) ? greeting.Data.Number.Greeting : string.Empty);
        }

        if (prompt.Contains("#{customer_items}", StringComparison.OrdinalIgnoreCase))
        {
            var soldToIds = !string.IsNullOrEmpty(assistant.Name) ? assistant.Name.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList() : [];

            if (soldToIds.Count > 0)
            {
                var caches = await _salesDataProvider.GetCustomerItemsCacheBySoldToIdsAsync(soldToIds, cancellationToken).ConfigureAwait(false);
                var items = caches.Where(c => !string.IsNullOrEmpty(c.CacheValue)).Select(c => c.CacheValue.Trim()).Distinct().Take(50);
                prompt = prompt.Replace("#{customer_items}", string.Join(Environment.NewLine + Environment.NewLine, items));
            }
        }

        if (agentId.HasValue && prompt.Contains("#{menu_items}", StringComparison.OrdinalIgnoreCase))
        {
            var menuItems = await GenerateMenuItemsAsync(agentId.Value, cancellationToken).ConfigureAwait(false);
            prompt = prompt.Replace("#{menu_items}", menuItems ?? "");
        }

        if (prompt.Contains("#{customer_info}", StringComparison.OrdinalIgnoreCase))
        {
            var cache = await _salesDataProvider.GetCustomerInfoCacheByPhoneNumberAsync(from, cancellationToken).ConfigureAwait(false);
            prompt = prompt.Replace("#{customer_info}", cache?.CacheValue?.Trim() ?? " ");
        }

        return prompt;
    }

    #endregion

    #region Routing

    private (string forwardNumber, int? forwardAssistantId) DecideDestinationByInboundRoute(List<AiSpeechAssistantInboundRoute> routes)
    {
        if (routes == null || routes.Count == 0)
            return (null, null);

        if (routes.Any(x => x.Emergency))
            routes = routes.Where(x => x.Emergency).ToList();

        foreach (var rule in routes)
        {
            var localNow = ConvertToRuleLocalTime(_clock.Now, rule.TimeZone);

            var days = ParseDays(rule.DayOfWeek) ?? [];
            var dayOk = days.Count == 0 || days.Contains(localNow.DayOfWeek);
            if (!dayOk) continue;

            var timeOk = rule.IsFullDay || IsWithinTimeWindow(localNow.TimeOfDay, rule.StartTime, rule.EndTime);
            if (!timeOk) continue;

            if (!string.IsNullOrWhiteSpace(rule.ForwardNumber))
                return (rule.ForwardNumber, null);

            if (rule.ForwardAssistantId.HasValue)
                return (null, rule.ForwardAssistantId.Value);
        }

        return (null, null);
    }

    private static DateTime ConvertToRuleLocalTime(DateTimeOffset utcNow, string timeZoneId)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(timeZoneId))
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                return TimeZoneInfo.ConvertTime(utcNow.UtcDateTime, tz);
            }
        }
        catch
        {
            return utcNow.UtcDateTime;
        }
        return utcNow.UtcDateTime;
    }

    private static List<DayOfWeek> ParseDays(string dayString)
    {
        if (string.IsNullOrWhiteSpace(dayString)) return [];

        var list = new List<DayOfWeek>();
        foreach (var token in dayString.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(token, out var v) && v is >= 0 and <= 6)
                list.Add((DayOfWeek)v);
        }
        return list;
    }

    private static bool IsWithinTimeWindow(TimeSpan localTime, TimeSpan? start, TimeSpan? end)
    {
        var startTime = start ?? TimeSpan.MinValue;
        var endTime = end ?? TimeSpan.MaxValue;

        if (startTime == endTime) return false;

        if (startTime < endTime) return localTime >= startTime && localTime <= endTime;

        return localTime >= startTime || localTime <= endTime;
    }

    #endregion

    #region Model Config & Session Options

    private async Task<RealtimeAiModelConfig> BuildModelConfigAsync(
        string resolvedPrompt, CancellationToken cancellationToken)
    {
        var assistant = _ctx.Assistant;

        var functionCalls = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantFunctionCallByAssistantIdsAsync(
                [assistant.Id], assistant.ModelProvider, true, cancellationToken).ConfigureAwait(false);

        var configs = functionCalls
            .Where(x => !string.IsNullOrWhiteSpace(x.Content))
            .Select(x => (x.Type, Config: JsonConvert.DeserializeObject<object>(x.Content)))
            .ToList();

        return new RealtimeAiModelConfig
        {
            Provider = assistant.ModelProvider,
            ServiceUrl = assistant.ModelUrl ?? AiSpeechAssistantStore.DefaultUrl,
            Voice = assistant.ModelVoice ?? "alloy",
            ModelName = assistant.ModelName,
            ModelLanguage = assistant.ModelLanguage,
            Prompt = resolvedPrompt,
            Tools = configs
                .Where(x => x.Type == AiSpeechAssistantSessionConfigType.Tool)
                .Select(x => x.Config)
                .ToList(),
            TurnDetection = configs.FirstOrDefault(x => x.Type == AiSpeechAssistantSessionConfigType.TurnDirection).Config,
            InputAudioNoiseReduction = configs.FirstOrDefault(x => x.Type == AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction).Config
        };
    }

    private RealtimeSessionOptions BuildSessionOptions(
        RealtimeAiModelConfig modelConfig,
        AiSpeechAssistantTimer timer)
    {
        return new RealtimeSessionOptions
        {
            ClientConfig = new RealtimeAiClientConfig
            {
                Client = RealtimeAiClient.Twilio
            },
            ModelConfig = modelConfig,
            ConnectionProfile = new RealtimeAiConnectionProfile
            {
                ProfileId = _ctx.Assistant.Id.ToString()
            },
            WebSocket = _ctx.TwilioWebSocket,
            Region = RealtimeAiServerRegion.US,
            EnableRecording = true,
            IdleFollowUp = timer != null
                ? new RealtimeSessionIdleFollowUp
                {
                    TimeoutSeconds = timer.TimeSpanSeconds,
                    FollowUpMessage = timer.AlterContent,
                    SkipRounds = timer.SkipRound
                }
                : null,
            OnSessionReadyAsync = async actions =>
            {
                await actions.SendTextToProviderAsync($"Greet the user with: '{_ctx.Knowledge?.Greetings}'").ConfigureAwait(false);
            },
            OnClientStartAsync = async (sessionId, metadata) =>
            {
                metadata.TryGetValue("callSid", out var callSid);
                metadata.TryGetValue("streamSid", out var streamSid);

                _ctx.CallSid = callSid;
                _ctx.StreamSid = streamSid;

                _backgroundJobClient.Enqueue<IMediator>(x => x.SendAsync(new RecordAiSpeechAssistantCallCommand
                {
                    CallSid = _ctx.CallSid, Host = _ctx.Host
                }, CancellationToken.None), HangfireConstants.InternalHostingRecordPhoneCall);

                if (!_ctx.IsInAiServiceHours && _ctx.IsEnableManualService)
                {
                    _backgroundJobClient.Enqueue<IMediator>(x => x.SendAsync(new TransferHumanServiceCommand
                    {
                        CallSid = _ctx.CallSid,
                        HumanPhone = _ctx.TransferCallNumber
                    }, CancellationToken.None));
                }
            },
            OnFunctionCallAsync = async (functionCallData, actions) =>
                await OnFunctionCallAsync(functionCallData, actions, CancellationToken.None).ConfigureAwait(false),
            OnTranscriptionsCompletedAsync = async (sessionId, transcriptions) =>
            {
                var streamContext = new AiSpeechAssistantStreamContextDto
                {
                    CallSid = _ctx.CallSid,
                    StreamSid = _ctx.StreamSid,
                    Host = _ctx.Host,
                    Assistant = _ctx.Assistant,
                    Knowledge = _ctx.Knowledge,
                    OrderItems = _ctx.OrderItems,
                    UserInfo = _ctx.UserInfo,
                    LastUserInfo = _ctx.LastUserInfo,
                    IsTransfer = _ctx.IsTransfer,
                    HumanContactPhone = _ctx.HumanContactPhone,
                    ConversationTranscription = transcriptions.Select(t => (t.Speaker, t.Text)).ToList()
                };

                _backgroundJobClient.Enqueue<IAiSpeechAssistantProcessJobService>(x =>
                    x.RecordAiSpeechAssistantCallAsync(streamContext, _ctx.OrderRecordType, CancellationToken.None));
            },
            OnRecordingCompleteAsync = async (sessionId, wavBytes) =>
            {
                Log.Information("[AiAssistant] Recording complete, SessionId: {SessionId}, Size: {Size}bytes",
                    sessionId, wavBytes.Length);
            }
        };
    }

    #endregion

    #region Menu Items

    private async Task<string> GenerateMenuItemsAsync(int agentId, CancellationToken cancellationToken = default)
    {
        var storeAgent = await _posDataProvider.GetPosAgentByAgentIdAsync(agentId, cancellationToken).ConfigureAwait(false);

        if (storeAgent == null) return null;

        var storeProducts = await _posDataProvider.GetPosProductsByAgentIdAsync(agentId, cancellationToken).ConfigureAwait(false);
        var storeCategories = (await _posDataProvider.GetPosCategoriesAsync(storeId: storeAgent.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false)).DistinctBy(x => x.CategoryId).ToList();

        var normalProducts = storeProducts.OrderBy(x => x.SortOrder).Where(x => x.Modifiers == "[]").Take(80).ToList();
        var modifierProducts = storeProducts.OrderBy(x => x.SortOrder).Where(x => x.Modifiers != "[]").Take(20).ToList();

        var partialProducts = normalProducts.Concat(modifierProducts).ToList();
        var categoryProductsLookup = new Dictionary<PosCategory, List<PosProduct>>();

        foreach (var product in partialProducts)
        {
            var category = storeCategories.FirstOrDefault(c => c.Id == product.CategoryId);
            if (category == null) continue;

            if (!categoryProductsLookup.ContainsKey(category))
                categoryProductsLookup[category] = new List<PosProduct>();

            categoryProductsLookup[category].Add(product);
        }

        var menuItems = string.Empty;

        foreach (var (category, products) in categoryProductsLookup)
        {
            if (products.Count == 0) continue;

            var productDetails = string.Empty;
            var categoryNames = JsonConvert.DeserializeObject<PosNamesLocalization>(category.Names);

            var categoryName = BuildMenuItemName(categoryNames);

            if (string.IsNullOrWhiteSpace(categoryName)) continue;

            var idx = 1;
            productDetails += categoryName + "\n";

            foreach (var product in products)
            {
                var productNames = JsonConvert.DeserializeObject<PosNamesLocalization>(product.Names);

                var productName = BuildMenuItemName(productNames);

                if (string.IsNullOrWhiteSpace(productName)) continue;
                var line = $"{idx}. {productName}：${product.Price:F2}";

                if (!string.IsNullOrEmpty(product.Modifiers))
                {
                    var modifiers = JsonConvert.DeserializeObject<List<EasyPosResponseModifierGroups>>(product.Modifiers);

                    if (modifiers is { Count: > 0 })
                    {
                        var modifiersDetail = string.Empty;

                        foreach (var modifier in modifiers)
                        {
                            var modifierNames = new List<string>();

                            if (modifier.ModifierProducts != null && modifier.ModifierProducts.Count != 0)
                            {
                                foreach (var mp in modifier.ModifierProducts)
                                {
                                    var name = BuildModifierName(mp.Localizations);

                                    if (!string.IsNullOrWhiteSpace(name)) modifierNames.Add($"{name}");
                                }
                            }

                            if (modifierNames.Count > 0)
                                modifiersDetail += $" {BuildModifierName(modifier.Localizations)}規格：{string.Join("、", modifierNames)}，共{modifierNames.Count}个规格，要求最少选{modifier.MinimumSelect}个规格，最多选{modifier.MaximumSelect}规格，每个最大可重复选{modifier.MaximumRepetition}相同的 \n";
                        }

                        line += modifiersDetail;
                    }
                }

                idx++;
                productDetails += line + "\n";
            }

            menuItems += productDetails + "\n";
        }

        return menuItems.TrimEnd('\r', '\n');
    }

    private static string BuildMenuItemName(PosNamesLocalization localization)
    {
        var zhName = !string.IsNullOrWhiteSpace(localization?.Cn?.Name) ? localization.Cn.Name : string.Empty;
        if (!string.IsNullOrWhiteSpace(zhName)) return zhName;

        var usName = !string.IsNullOrWhiteSpace(localization?.En?.Name) ? localization.En.Name : string.Empty;
        if (!string.IsNullOrWhiteSpace(usName)) return usName;

        var zhPosName = !string.IsNullOrWhiteSpace(localization?.Cn?.PosName) ? localization.Cn.PosName : string.Empty;
        if (!string.IsNullOrWhiteSpace(zhPosName)) return zhPosName;

        var usPosName = !string.IsNullOrWhiteSpace(localization?.En?.PosName) ? localization.En.PosName : string.Empty;
        if (!string.IsNullOrWhiteSpace(usPosName)) return usPosName;

        var zhSendChefName = !string.IsNullOrWhiteSpace(localization?.Cn?.SendChefName) ? localization.Cn.SendChefName : string.Empty;
        if (!string.IsNullOrWhiteSpace(zhSendChefName)) return zhSendChefName;

        var usSendChefName = !string.IsNullOrWhiteSpace(localization?.En?.SendChefName) ? localization.En.SendChefName : string.Empty;
        if (!string.IsNullOrWhiteSpace(usSendChefName)) return usSendChefName;

        return string.Empty;
    }

    private static string BuildModifierName(List<EasyPosResponseLocalization> localizations)
    {
        var zhName = localizations.Find(l => l.LanguageCode == "zh_CN" && l.Field == "name");
        if (zhName != null && !string.IsNullOrWhiteSpace(zhName.Value)) return zhName.Value;

        var usName = localizations.Find(l => l.LanguageCode == "en_US" && l.Field == "name");
        if (usName != null && !string.IsNullOrWhiteSpace(usName.Value)) return usName.Value;

        var zhPosName = localizations.Find(l => l.LanguageCode == "zh_CN" && l.Field == "posName");
        if (zhPosName != null && !string.IsNullOrWhiteSpace(zhPosName.Value)) return zhPosName.Value;

        var usPosName = localizations.Find(l => l.LanguageCode == "en_US" && l.Field == "posName");
        if (usPosName != null && !string.IsNullOrWhiteSpace(usPosName.Value)) return usPosName.Value;

        var zhSendChefName = localizations.Find(l => l.LanguageCode == "zh_CN" && l.Field == "sendChefName");
        if (zhSendChefName != null && !string.IsNullOrWhiteSpace(zhSendChefName.Value)) return zhSendChefName.Value;

        var usSendChefName = localizations.Find(l => l.LanguageCode == "en_US" && l.Field == "sendChefName");
        if (usSendChefName != null && !string.IsNullOrWhiteSpace(usSendChefName.Value)) return usSendChefName.Value;

        return string.Empty;
    }

    #endregion
}
