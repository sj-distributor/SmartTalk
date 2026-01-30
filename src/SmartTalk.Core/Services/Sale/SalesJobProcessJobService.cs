using Serilog;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Messages.Commands.Sales;

namespace SmartTalk.Core.Services.Sale;

public interface ISalesJobProcessJobService : IScopedDependency
{
    Task ScheduleRefreshCustomerItemsCacheAsync(RefreshAllCustomerItemsCacheCommand command, CancellationToken cancellationToken);

    Task RefreshCustomerItemsCacheBySoldToIdAsync(string soldToId, CancellationToken cancellationToken);

    Task ScheduleRefreshCrmCustomerInfoAsync(RefreshAllCustomerInfoCacheCommand command, CancellationToken cancellationToken);

    Task RefreshCrmCustomerInfoByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken);
}

public class SalesJobProcessJobService : ISalesJobProcessJobService
{
    private readonly ICrmClient _crmClient;
    private readonly ISalesService _salesService;
    private readonly ISalesDataProvider _salesDataProvider;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    
    public SalesJobProcessJobService(ICrmClient crmClient, ISalesService salesService, ISalesDataProvider salesDataProvider, ISmartTalkBackgroundJobClient backgroundJobClient, SpeechMaticsDataProvider speechMaticsDataProvide)
    {
        _crmClient = crmClient;
        _salesService = salesService;
        _salesDataProvider = salesDataProvider;
        _backgroundJobClient = backgroundJobClient;
    }
    
    public async Task ScheduleRefreshCustomerItemsCacheAsync(RefreshAllCustomerItemsCacheCommand command, CancellationToken cancellationToken)
    {
        Log.Information("Start full customer items cache refresh...");

        var allSales = await _salesDataProvider.GetAllSalesAsync(cancellationToken).ConfigureAwait(false);
        var allSoldToIds = allSales.Select(s => s.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();

        foreach (var soldToId in allSoldToIds)
        {
            _backgroundJobClient.Enqueue<ISalesJobProcessJobService>(x => x.RefreshCustomerItemsCacheBySoldToIdAsync(soldToId, CancellationToken.None), HangfireConstants.InternalHostingCaCheKnowledgeVariable);
        }

        Log.Information("All customer items cache refresh jobs scheduled. Count: {Count}", allSoldToIds.Count);
    }

    public async Task RefreshCustomerItemsCacheBySoldToIdAsync(string soldToId, CancellationToken cancellationToken)
    {
        try
        {
            var ids = soldToId.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
            Log.Information("Refreshing cache for soldToId: {SoldToId}", ids);

            var combinedItems = await _salesService.BuildCustomerItemsStringAsync(ids, cancellationToken).ConfigureAwait(false);

            await _salesDataProvider.UpsertCustomerItemsCacheAsync(soldToId, combinedItems, true, cancellationToken).ConfigureAwait(false);

            Log.Information("Cache refreshed successfully for soldToId: {SoldToId}", soldToId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to refresh cache for soldToId: {SoldToId}", soldToId);
        }
    }
    
    public async Task ScheduleRefreshCrmCustomerInfoAsync(RefreshAllCustomerInfoCacheCommand command, CancellationToken cancellationToken)
    {
        var allSales = await _salesDataProvider.GetAllSalesAsync(cancellationToken);
        var allSoldToIds = allSales.Select(s => s.Name).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
        
        var crmToken = await _crmClient.GetCrmTokenAsync(cancellationToken).ConfigureAwait(false);
        if (crmToken == null) return;
        
        var totalPhones = 0;
        
        foreach (var soldToId in allSoldToIds)
        {
            var contacts = await _crmClient.GetCustomerContactsAsync(soldToId, crmToken, cancellationToken).ConfigureAwait(false);

            var phoneNumbers = contacts?.Where(c => !string.IsNullOrEmpty(c.Phone)).Select(c => NormalizePhone(c.Phone)).Distinct().ToList() ?? new List<string>();

            totalPhones += phoneNumbers.Count;
            
            foreach (var phone in phoneNumbers)
            {
                _backgroundJobClient.Enqueue<ISalesJobProcessJobService>(x => x.RefreshCrmCustomerInfoByPhoneNumberAsync(phone, CancellationToken.None), HangfireConstants.InternalHostingCaCheKnowledgeVariable);
            }
        }

        Log.Information("Scheduled CRM customer info refresh for {CustomerCount} customers, {PhoneCount} phone numbers", allSoldToIds.Count, totalPhones);
    }

    
    public async Task RefreshCrmCustomerInfoByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        try
        {
            Log.Information("Refreshing CRM customer info cache for phone {Phone}", phoneNumber);

            var normalizedPhone = NormalizePhone(phoneNumber);
            var infoString = await _salesService.BuildCrmCustomerInfoByPhoneAsync(normalizedPhone, cancellationToken).ConfigureAwait(false);

            await _salesDataProvider.UpsertCustomerInfoCacheAsync(normalizedPhone, infoString, true, cancellationToken).ConfigureAwait(false);

            Log.Information("CRM customer info cached successfully for phone {Phone}", phoneNumber);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to refresh CRM customer info cache for phone {Phone}", phoneNumber);
        }
    }
    
    private string NormalizePhone(string phone)
    {
        if (string.IsNullOrEmpty(phone)) return phone;
        
        phone = phone.Replace("-", "").Replace(" ", "").Replace("(", "").Replace(")", "");
        
        if (!phone.StartsWith("+") && phone.Length == 10)
            phone = "+1" + phone;

        return phone;
    }
}