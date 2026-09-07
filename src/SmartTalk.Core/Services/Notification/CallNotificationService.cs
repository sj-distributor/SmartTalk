using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using OpenAI;
using OpenAI.Chat;
using Serilog;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Notification;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.Account;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Twilio;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Core.Middlewares.Authorization;
using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.Notification;
using SmartTalk.Messages.Requests.Notification;
using SmartTalk.Messages.Requests.Twilio;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Dto.Attachments;
using System.ClientModel;
using System.Text.RegularExpressions;

namespace SmartTalk.Core.Services.Notification;

public interface ICallNotificationService : IScopedDependency
{
    Task ProcessCallNotificationAsync(int phoneOrderRecordId, CancellationToken cancellationToken = default);
    
    Task RetryCallNotificationAsync(int notificationRecordId, CancellationToken cancellationToken = default);
    
    Task<List<CallNotificationScenarioRuleDto>> GetScenarioRulesAsync(int? storeId, CancellationToken cancellationToken = default);
    
    Task UpsertScenarioRuleAsync(UpsertCallNotificationScenarioRuleRequest request, CancellationToken cancellationToken = default);
    
    Task UpdateStoreSettingAsync(UpdateStoreCallNotificationSettingRequest request, CancellationToken cancellationToken = default);
    
    Task<(int Count, List<CallNotificationRecordDto> Records)> GetRecordsAsync(GetCallNotificationRecordsRequest request, CancellationToken cancellationToken = default);
    
    Task<string> ExportRecordsAsync(GetCallNotificationRecordsRequest request, CancellationToken cancellationToken = default);
}

public class CallNotificationService : ICallNotificationService
{
    private const int MaximumGatewayRetryCount = 3;
    private const int MaximumSmsLength = 1600;
    private readonly ICallNotificationDataProvider _dataProvider;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly IPosDataProvider _posDataProvider;
    private readonly ISmsOptOutService _smsOptOutService;
    private readonly ITwilioService _twilioService;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly OpenAiSettings _openAiSettings;
    private readonly IAttachmentService _attachmentService;
    private readonly ICurrentUser _currentUser;
    private readonly IAccountDataProvider _accountDataProvider;

    public CallNotificationService(
        ICallNotificationDataProvider dataProvider,
        IPhoneOrderDataProvider phoneOrderDataProvider,
        IPosDataProvider posDataProvider,
        ISmsOptOutService smsOptOutService,
        ITwilioService twilioService,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        OpenAiSettings openAiSettings,
        IAttachmentService attachmentService,
        ICurrentUser currentUser,
        IAccountDataProvider accountDataProvider)
    {
        _dataProvider = dataProvider;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _posDataProvider = posDataProvider;
        _smsOptOutService = smsOptOutService;
        _twilioService = twilioService;
        _backgroundJobClient = backgroundJobClient;
        _openAiSettings = openAiSettings;
        _attachmentService = attachmentService;
        _currentUser = currentUser;
        _accountDataProvider = accountDataProvider;
    }

    public async Task ProcessCallNotificationAsync(int phoneOrderRecordId, CancellationToken cancellationToken = default)
    {
        var existing = await _dataProvider.GetRecordByCallAsync(phoneOrderRecordId, cancellationToken).ConfigureAwait(false);
        if (existing != null)
            return;

        var record = await _phoneOrderDataProvider.GetPhoneOrderRecordByIdAsync(phoneOrderRecordId, cancellationToken).ConfigureAwait(false);
        if (record == null)
            return;

        var store = await _posDataProvider.GetPosStoreByAgentIdAsync(record.AgentId, cancellationToken).ConfigureAwait(false);
        if (store == null)
        {
            Log.Warning("Skip call notification because no store is mapped to AgentId {AgentId}.", record.AgentId);
            return;
        }

        var scenarioKey = GetScenarioKey(record);
        var setting = await _dataProvider.GetStoreSettingAsync(store.Id, scenarioKey, cancellationToken).ConfigureAwait(false);
        if (setting?.IsEnabled != true)
            return;

        var (senderNumber, customerNumber) = GetSmsNumbers(record);
        if (string.IsNullOrWhiteSpace(senderNumber) || string.IsNullOrWhiteSpace(customerNumber))
        {
            await CreateTerminalRecordAsync(record, store, scenarioKey, senderNumber, customerNumber,
                CallNotificationStatus.Failed, "Customer or merchant phone number is missing.", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (await _smsOptOutService.IsOptedOutAsync(store.CompanyId, customerNumber, cancellationToken).ConfigureAwait(false))
        {
            await CreateTerminalRecordAsync(record, store, scenarioKey, senderNumber, customerNumber,
                CallNotificationStatus.Skipped, "Customer opted out of SMS notifications.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var rule = await _dataProvider.GetScenarioRuleAsync(scenarioKey, true, cancellationToken).ConfigureAwait(false);
        if (rule == null)
        {
            await CreateTerminalRecordAsync(record, store, scenarioKey, senderNumber, customerNumber,
                CallNotificationStatus.Failed, $"No active notification rule is configured for scenario '{scenarioKey}'.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var originalReport = await _dataProvider.GetOriginReportAsync(record.Id, cancellationToken).ConfigureAwait(false);
        originalReport ??= record.TranscriptionText;

        string content;
        try
        {
            content = await GenerateSmsAsync(rule, store.Names, originalReport, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Call notification content generation failed. RecordId: {RecordId}", record.Id);
            await CreateTerminalRecordAsync(record, store, scenarioKey, senderNumber, customerNumber,
                CallNotificationStatus.Failed, $"AI generation failed: {exception.Message}", cancellationToken).ConfigureAwait(false);
            return;
        }

        var notification = new CallNotificationRecord
        {
            PhoneOrderRecordId = record.Id,
            CompanyId = store.CompanyId,
            StoreId = store.Id,
            AgentId = record.AgentId,
            ScenarioKey = scenarioKey,
            SenderNumber = SmsOptOutService.NormalizePhoneNumber(senderNumber),
            CustomerNumber = SmsOptOutService.NormalizePhoneNumber(customerNumber),
            Content = content,
            Status = CallNotificationStatus.Processing,
            CreatedDate = DateTimeOffset.UtcNow
        };

        await _dataProvider.AddAsync(notification, cancellationToken).ConfigureAwait(false);
        await _dataProvider.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await SendNotificationAsync(notification, cancellationToken).ConfigureAwait(false);
    }

    public async Task RetryCallNotificationAsync(int notificationRecordId, CancellationToken cancellationToken = default)
    {
        var notification = await _dataProvider.GetRecordAsync(notificationRecordId, cancellationToken).ConfigureAwait(false);
        if (notification == null || notification.Status != CallNotificationStatus.Processing)
            return;

        await SendNotificationAsync(notification, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<CallNotificationScenarioRuleDto>> GetScenarioRulesAsync(int? storeId, CancellationToken cancellationToken = default)
    {
        if (storeId.HasValue)
            await EnsureStoreAccessAsync(storeId.Value, cancellationToken).ConfigureAwait(false);
        else
            await EnsureServiceProviderAccessAsync(cancellationToken).ConfigureAwait(false);

        var rules = await _dataProvider.GetScenarioRulesAsync(cancellationToken).ConfigureAwait(false);
        var settings = storeId.HasValue
            ? await _dataProvider.GetStoreSettingsAsync(storeId.Value, cancellationToken).ConfigureAwait(false)
            : [];

        return GetScenarioDefinitions()
            .Select(definition =>
            {
                var rule = rules.SingleOrDefault(x => x.ScenarioKey == definition.Key);
                var setting = settings.SingleOrDefault(x => x.ScenarioKey == definition.Key);
                return new CallNotificationScenarioRuleDto
                {
                    ScenarioKey = definition.Key,
                    ScenarioName = definition.Name,
                    Description = rule?.Description ?? string.Empty,
                    PromptTemplate = rule?.PromptTemplate ?? string.Empty,
                    IsActive = rule?.IsActive ?? false,
                    IsEnabled = setting?.IsEnabled ?? false
                };
            })
            .ToList();
    }

    public async Task UpsertScenarioRuleAsync(UpsertCallNotificationScenarioRuleRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureServiceProviderAccessAsync(cancellationToken).ConfigureAwait(false);
        ValidateScenarioKey(request.ScenarioKey);
        if (string.IsNullOrWhiteSpace(request.Description) || string.IsNullOrWhiteSpace(request.PromptTemplate))
            throw new ArgumentException("Scenario description and prompt template are required.");

        var rule = await _dataProvider.GetScenarioRuleAsync(request.ScenarioKey, false, cancellationToken).ConfigureAwait(false);
        if (rule == null)
        {
            await _dataProvider.AddAsync(new CallNotificationScenarioRule
            {
                ScenarioKey = request.ScenarioKey,
                Description = request.Description.Trim(),
                PromptTemplate = request.PromptTemplate.Trim(),
                IsActive = request.IsActive,
                CreatedDate = DateTimeOffset.UtcNow
            }, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            rule.Description = request.Description.Trim();
            rule.PromptTemplate = request.PromptTemplate.Trim();
            rule.IsActive = request.IsActive;
            rule.LastModifiedDate = DateTimeOffset.UtcNow;
            await _dataProvider.UpdateAsync(rule, cancellationToken).ConfigureAwait(false);
        }

        await _dataProvider.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateStoreSettingAsync(UpdateStoreCallNotificationSettingRequest request, CancellationToken cancellationToken = default)
    {
        ValidateScenarioKey(request.ScenarioKey);
        await EnsureStoreAccessAsync(request.StoreId, cancellationToken).ConfigureAwait(false);
        var store = await _posDataProvider.GetPosCompanyStoreAsync(id: request.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Store {request.StoreId} does not exist.");

        var setting = await _dataProvider.GetStoreSettingAsync(request.StoreId, request.ScenarioKey, cancellationToken).ConfigureAwait(false);
        if (setting == null)
        {
            await _dataProvider.AddAsync(new StoreCallNotificationSetting
            {
                CompanyId = store.CompanyId,
                StoreId = store.Id,
                ScenarioKey = request.ScenarioKey,
                IsEnabled = request.IsEnabled,
                CreatedDate = DateTimeOffset.UtcNow
            }, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            setting.IsEnabled = request.IsEnabled;
            setting.LastModifiedDate = DateTimeOffset.UtcNow;
            await _dataProvider.UpdateAsync(setting, cancellationToken).ConfigureAwait(false);
        }

        await _dataProvider.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(int Count, List<CallNotificationRecordDto> Records)> GetRecordsAsync(GetCallNotificationRecordsRequest request, CancellationToken cancellationToken = default)
    {
        var accessScope = await GetAccessScopeAsync(cancellationToken).ConfigureAwait(false);
        return await _dataProvider.GetRecordsAsync(request, accessScope.StoreIds, accessScope.IsServiceProvider, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ExportRecordsAsync(GetCallNotificationRecordsRequest request, CancellationToken cancellationToken = default)
    {
        var accessScope = await GetAccessScopeAsync(cancellationToken).ConfigureAwait(false);
        var (_, records) = await _dataProvider.GetRecordsAsync(request, accessScope.StoreIds, accessScope.IsServiceProvider, false, cancellationToken).ConfigureAwait(false);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Notifications");
        var headers = new[] { "Time", "Company ID", "Store ID", "Scenario", "Customer Number", "Channel", "Status", "Content", "Failure Reason", "Retry Count" };
        for (var index = 0; index < headers.Length; index++)
            worksheet.Cell(1, index + 1).Value = headers[index];

        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            var row = index + 2;
            worksheet.Cell(row, 1).Value = record.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss");
            worksheet.Cell(row, 2).Value = record.CompanyId;
            worksheet.Cell(row, 3).Value = record.StoreId;
            worksheet.Cell(row, 4).Value = record.ScenarioKey;
            worksheet.Cell(row, 5).Value = record.CustomerNumber;
            worksheet.Cell(row, 6).Value = record.Channel;
            worksheet.Cell(row, 7).Value = record.Status.ToString();
            worksheet.Cell(row, 8).Value = record.Content;
            worksheet.Cell(row, 9).Value = record.FailureReason;
            worksheet.Cell(row, 10).Value = record.RetryCount;
        }

        worksheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var upload = await _attachmentService.UploadAttachmentAsync(new UploadAttachmentCommand
        {
            Attachment = new UploadAttachmentDto
            {
                FileName = $"call-notifications-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.xlsx",
                FileContent = stream.ToArray()
            }
        }, cancellationToken).ConfigureAwait(false);

        return upload.Attachment?.FileUrl ?? string.Empty;
    }

    private async Task SendNotificationAsync(CallNotificationRecord notification, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _twilioService.SendMessageAsync(new SendTwilioMessageRequest
            {
                CompanyId = notification.CompanyId,
                FromNumber = notification.SenderNumber,
                ToNumber = notification.CustomerNumber,
                Body = notification.Content
            }, cancellationToken).ConfigureAwait(false);

            notification.LastModifiedDate = DateTimeOffset.UtcNow;
            if (response.IsSkipped)
            {
                notification.Status = CallNotificationStatus.Skipped;
                notification.FailureReason = response.SkipReason;
            }
            else
            {
                notification.Status = CallNotificationStatus.Succeeded;
                notification.TwilioMessageSid = response.Sid;
                notification.SentDate = DateTimeOffset.UtcNow;
                notification.FailureReason = null;
            }

            await _dataProvider.UpdateAsync(notification, cancellationToken).ConfigureAwait(false);
            await _dataProvider.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            notification.RetryCount++;
            notification.FailureReason = Truncate(exception.Message, 2048);
            notification.LastModifiedDate = DateTimeOffset.UtcNow;
            notification.Status = notification.RetryCount >= MaximumGatewayRetryCount
                ? CallNotificationStatus.Failed
                : CallNotificationStatus.Processing;

            await _dataProvider.UpdateAsync(notification, cancellationToken).ConfigureAwait(false);
            await _dataProvider.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (notification.Status == CallNotificationStatus.Processing)
                _backgroundJobClient.Schedule<ICallNotificationService>(
                    service => service.RetryCallNotificationAsync(notification.Id, CancellationToken.None),
                    TimeSpan.FromSeconds(30));
        }
    }

    private async Task CreateTerminalRecordAsync(
        PhoneOrderRecord record,
        Domain.Pos.CompanyStore store,
        string scenarioKey,
        string senderNumber,
        string customerNumber,
        CallNotificationStatus status,
        string reason,
        CancellationToken cancellationToken)
    {
        await _dataProvider.AddAsync(new CallNotificationRecord
        {
            PhoneOrderRecordId = record.Id,
            CompanyId = store.CompanyId,
            StoreId = store.Id,
            AgentId = record.AgentId,
            ScenarioKey = scenarioKey,
            SenderNumber = string.IsNullOrWhiteSpace(senderNumber) ? string.Empty : SmsOptOutService.NormalizePhoneNumber(senderNumber),
            CustomerNumber = string.IsNullOrWhiteSpace(customerNumber) ? string.Empty : SmsOptOutService.NormalizePhoneNumber(customerNumber),
            Status = status,
            FailureReason = Truncate(reason, 2048),
            CreatedDate = DateTimeOffset.UtcNow
        }, cancellationToken).ConfigureAwait(false);
        await _dataProvider.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GenerateSmsAsync(CallNotificationScenarioRule rule, string storeName, string report, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(report))
            throw new InvalidOperationException("The original call analysis report is empty.");

        var client = new ChatClient(_openAiSettings.RecordAnalyzeModel, new ApiKeyCredential(_openAiSettings.ApiKey));
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                "Generate one factual SMS using only the supplied call analysis report. Do not invent facts. " +
                "Return only the SMS body, without quotation marks and without a STOP instruction."),
            new UserChatMessage(
                $"Scenario description:\n{rule.Description}\n\nPrompt template:\n{rule.PromptTemplate}\n\n" +
                $"Merchant name:\n{storeName}\n\nOriginal call analysis report:\n{report}")
        };
        var completion = (await client.CompleteChatAsync(messages, new ChatCompletionOptions
        {
            ResponseModalities = ChatResponseModalities.Text
        }, cancellationToken).ConfigureAwait(false)).Value;

        var generated = completion.Content.FirstOrDefault()?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(generated))
            throw new InvalidOperationException("Generated notification content is empty.");
        if (generated.Any(char.IsControl))
            throw new InvalidOperationException("Generated notification content contains invalid control characters.");

        var stopText = ContainsChinese(report) ? " 回复 STOP 退订。" : " Reply STOP to opt out.";
        var content = $"{generated}{stopText}";
        if (content.Length > MaximumSmsLength)
            throw new InvalidOperationException($"Generated notification content exceeds {MaximumSmsLength} characters.");

        return content;
    }

    private static (string SenderNumber, string CustomerNumber) GetSmsNumbers(PhoneOrderRecord record)
    {
        return record.OrderRecordType == PhoneOrderRecordType.OutBount
            ? (record.PhoneNumber, record.IncomingCallNumber)
            : (record.IncomingCallNumber, record.PhoneNumber);
    }

    private static string GetScenarioKey(PhoneOrderRecord record)
    {
        return record.OrderRecordType == PhoneOrderRecordType.OutBount
            ? "call_out"
            : record.Scenario?.ToString().ToLowerInvariant() ?? "other";
    }

    private static bool ContainsChinese(string value) => !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, @"\p{IsCJKUnifiedIdeographs}");

    private static void ValidateScenarioKey(string scenarioKey)
    {
        if (!GetScenarioDefinitions().Any(x => x.Key == scenarioKey))
            throw new ArgumentException($"Unknown notification scenario key '{scenarioKey}'.", nameof(scenarioKey));
    }

    private static string Truncate(string value, int maximumLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maximumLength ? value : value[..maximumLength];

    private async Task EnsureServiceProviderAccessAsync(CancellationToken cancellationToken)
    {
        var accessScope = await GetAccessScopeAsync(cancellationToken).ConfigureAwait(false);
        if (!accessScope.IsServiceProvider)
            throw new ForbiddenAccessException();
    }

    private async Task EnsureStoreAccessAsync(int storeId, CancellationToken cancellationToken)
    {
        var accessScope = await GetAccessScopeAsync(cancellationToken).ConfigureAwait(false);
        if (!accessScope.IsServiceProvider && !accessScope.StoreIds.Contains(storeId))
            throw new ForbiddenAccessException();
    }

    private async Task<NotificationAccessScope> GetAccessScopeAsync(CancellationToken cancellationToken)
    {
        if (!_currentUser.Id.HasValue)
            throw new ForbiddenAccessException();

        var userAccount = await _accountDataProvider
            .GetUserAccountByUserIdAsync(_currentUser.Id.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (userAccount == null)
            throw new ForbiddenAccessException();

        if (userAccount.AccountLevel == UserAccountLevel.ServiceProvider)
            return new NotificationAccessScope(true, []);

        var storeUsers = await _posDataProvider
            .GetPosStoreUsersByUserIdAsync(_currentUser.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        return new NotificationAccessScope(false, storeUsers.Select(x => x.StoreId).Distinct().ToList());
    }

    private static IReadOnlyList<(string Key, string Name)> GetScenarioDefinitions() =>
        Enum.GetValues<DialogueScenarios>()
            .Where(x => x != DialogueScenarios.ToDoTask)
            .Select(x => (x.ToString().ToLowerInvariant(), x.GetDescription()))
            .Append(("call_out", "外呼"))
            .ToList();

    private sealed record NotificationAccessScope(bool IsServiceProvider, List<int> StoreIds);
}
