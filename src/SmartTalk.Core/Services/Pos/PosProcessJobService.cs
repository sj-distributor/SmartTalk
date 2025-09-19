using Serilog;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.Pos;
using SmartTalk.Messages.Enums.Pos;

namespace SmartTalk.Core.Services.Pos;

public interface IPosProcessJobService : IScopedDependency
{
    Task SyncCustomerInfoFromOrderAsync(SchedulingSyncCustomerInfoCommand command, CancellationToken cancellationToken = default);
}

public class PosProcessJobService : IPosProcessJobService
{
    private readonly IPosDataProvider _posDataProvider;

    public PosProcessJobService(IPosDataProvider posDataProvider)
    {
        _posDataProvider = posDataProvider;
    }

    public async Task SyncCustomerInfoFromOrderAsync(SchedulingSyncCustomerInfoCommand command, CancellationToken cancellationToken = default)
    {
        var orders = await _posDataProvider.GetPosCustomerInfosAsync(cancellationToken).ConfigureAwait(false);

        Log.Information("Get orders: {@Orders} for customer ...", orders);
        
        if (orders == null || orders.Count == 0) return;
        
        var customers = orders.GroupBy(x => x.Phone).Select(x => new StoreCustomer
        {
            StoreId = x.First().StoreId,
            Name = x.OrderByDescending(o => o.CreatedDate).First().Name,
            Phone = x.Key,
            Address = x.Where(o => o.Type == PosOrderReceiveType.Delivery).FirstOrDefault()?.Address,
            Latitude = x.Where(o => o.Type == PosOrderReceiveType.Delivery).FirstOrDefault()?.Latitude,
            Longitude = x.Where(o => o.Type == PosOrderReceiveType.Delivery).FirstOrDefault()?.Longitude,
            Room = x.Where(o => o.Type == PosOrderReceiveType.Delivery).FirstOrDefault()?.Room,
            Remarks = x.Where(o => o.Type == PosOrderReceiveType.Delivery).FirstOrDefault()?.Remarks
        }).ToList();
        
        Log.Information("Syncing customer info from order: {@CustomerInfos}]", customers);
        
        await _posDataProvider.AddStoreCustomersAsync(customers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}