using AutoMapper;
using AutoMapper.QueryableExtensions;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Data;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Printer;
using SmartTalk.Messages.Enums.Printer;
using SmartTalk.Messages.Requests.Printer;

namespace SmartTalk.Core.Services.Printer;

public interface IPrinterDataProvider : IScopedDependency
{
    Task<List<MerchPrinter>> GetMerchPrintersAsync(string printerMac = null, Guid? token = null,
        int? storeId = null, int? id = null, bool? isEnabled = null, DateTimeOffset? lastStatusInfoLastModifiedDate = null, bool? IsStatusInfo = null, CancellationToken cancellationToken = default);

    Task<List<MerchPrinterOrder>> GetMerchPrinterOrdersAsync(Guid? jobToken = null, int? storeId = null, PrintStatus? status = null,
        DateTimeOffset? endTime = null, string printerMac = null, bool isOrderByPrintDate = false, int? orderId = null, Guid? id = null, int? recordId = null, CancellationToken cancellationToken = default);

    Task UpdateMerchPrinterOrderAsync(MerchPrinterOrder merchPrinterOrder, bool forceSave = true, CancellationToken cancellationToken = default);

    Task UpdateMerchPrinterMacAsync(MerchPrinter merchPrinter, bool forceSave = true, CancellationToken cancellationToken = default);

    Task AddMerchPrinterOrderAsync(MerchPrinterOrder merchPrinterOrder, CancellationToken cancellationToken);

    Task AddMerchPrinterLogAsync(MerchPrinterLog merchPrinterLog, CancellationToken cancellationToken);
    
    Task<PrinterToken> GetPrinterTokenAsync(string printerMac, CancellationToken cancellationToken);

    Task AddPrinterTokenAsync(PrinterToken printerToken, bool foreSave = true, CancellationToken cancellationToken = default);

    Task AddMerchPrinterAsync(MerchPrinter merchPrinter, bool foreSave = true, CancellationToken cancellationToken = default);

    Task DeleteMerchPrinterAsync(MerchPrinter merchPrinter, bool foreSave = true, CancellationToken cancellationToken = default);

    Task<(int, List<MerchPrinterLogDto>)> GetMerchPrinterLogAsync(int storeId, string printerMac = null, DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null, int? code = null, PrintLogType? logType = null, int? pageIndex = null, int? pageSize = null, int? orderId = null, int? recordId = null, CancellationToken cancellationToken = default);
}

public class PrinterDataProvider : IPrinterDataProvider
{
    private readonly IMapper _mapper;
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public PrinterDataProvider(IMapper mapper, IRepository repository, IUnitOfWork unitOfWork)
    {
        _mapper = mapper;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<MerchPrinter>> GetMerchPrintersAsync(string printerMac = null, Guid? token = null,
        int? storeId = null, int? id = null, bool? isEnabled = null, DateTimeOffset? lastStatusInfoLastModifiedDate = null, bool? IsStatusInfo = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<MerchPrinter>();

        if (id.HasValue)
            query = query.Where(x => x.Id == id.Value);
            
        if (!string.IsNullOrEmpty(printerMac))
            query = query.Where(x => x.PrinterMac == printerMac);

        if (token.HasValue)
            query = query.Where(x => x.Token == token.Value);

        if (storeId.HasValue)
            query = query.Where(x => x.StoreId == storeId.Value);

        if (isEnabled.HasValue)
            query = query.Where(x => x.IsEnabled == isEnabled.Value);

        if (lastStatusInfoLastModifiedDate.HasValue)
            query = query.Where(x => x.StatusInfoLastModifiedDate < lastStatusInfoLastModifiedDate.Value);

        if (IsStatusInfo.HasValue)
            query = query.Where(x => !string.IsNullOrEmpty(x.StatusInfo));
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<List<MerchPrinterOrder>> GetMerchPrinterOrdersAsync(Guid? jobToken = null, int? storeId = null, PrintStatus? status = null,
        DateTimeOffset? endTime = null, string printerMac = null, bool isOrderByPrintDate = false, int? orderId = null, Guid? id = null, int? recordId = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<MerchPrinterOrder>();

        if (jobToken.HasValue)
            query = query.Where(x => x.Id == jobToken.Value);

        if (storeId.HasValue)
            query = query.Where(x => x.StoreId == storeId.Value);

        if (recordId.HasValue)
            query = query.Where(x => x.RecordId == recordId);

        if (status.HasValue)
            query = query.Where(x => x.PrintStatus == status.Value);

        if (endTime.HasValue)
            query = query.Where(x => x.PrintDate <= endTime.Value);

        if (!string.IsNullOrEmpty(printerMac))
            query = query.Where(x => x.PrinterMac == printerMac);

        if (isOrderByPrintDate)
            query = query.OrderBy(x => x.PrintDate);

        if (orderId.HasValue)
            query = query.Where(x => x.OrderId == orderId);

        if (id.HasValue)
            query = query.Where(x => x.Id == id);

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateMerchPrinterOrderAsync(MerchPrinterOrder merchPrinterOrder, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(merchPrinterOrder, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateMerchPrinterMacAsync(MerchPrinter merchPrinter, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAsync(merchPrinter, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddMerchPrinterOrderAsync(MerchPrinterOrder merchPrinterOrder, CancellationToken cancellationToken)
    {
        await _repository.InsertAsync(merchPrinterOrder, cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddMerchPrinterLogAsync(MerchPrinterLog merchPrinterLog, CancellationToken cancellationToken)
    {
        await _repository.InsertAsync(merchPrinterLog, cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<PrinterToken> GetPrinterTokenAsync(string printerMac, CancellationToken cancellationToken)
    {
        return await _repository.FirstOrDefaultAsync<PrinterToken>(x => x.PrinterMac == printerMac, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddPrinterTokenAsync(PrinterToken printerToken, bool foreSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(printerToken, cancellationToken).ConfigureAwait(false);

        if (foreSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddMerchPrinterAsync(MerchPrinter merchPrinter, bool foreSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAsync(merchPrinter, cancellationToken).ConfigureAwait(false);

        if (foreSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteMerchPrinterAsync(MerchPrinter merchPrinter, bool foreSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(merchPrinter, cancellationToken).ConfigureAwait(false);

        if (foreSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(int, List<MerchPrinterLogDto>)> GetMerchPrinterLogAsync(int storeId, string printerMac = null, DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null, int? code = null, PrintLogType? logType = null, int? pageIndex = null, int? pageSize = null, int? orderId = null, int? recordId = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<MerchPrinterLog>().Where(x => x.StoreId == storeId);

        if (!string.IsNullOrEmpty(printerMac))
            query = query.Where(x => x.PrinterMac == printerMac);

        if (startDate.HasValue && endDate.HasValue)
            query = query.Where(x => x.CreatedDate >= startDate.Value && x.CreatedDate <= endDate.Value);

        if (code.HasValue)
            query = query.Where(x => x.Code == code.Value);

        if (logType.HasValue)
            query = query.Where(x => x.PrintLogType == logType.Value);

        if (orderId.HasValue)
            query = query.Where(x => x.OrderId == orderId.Value);

        if (recordId.HasValue)
            query = query.Where(x => x.PhoneOrderId == recordId.Value);

        var count = query.Count();

        if (count <= 0)
            return (0, new List<MerchPrinterLogDto>());

        if (pageIndex.HasValue && pageSize.HasValue)
            query = query.OrderByDescending(x => x.CreatedDate)
                .Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
        
        var results = await query
            .ProjectTo<MerchPrinterLogDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (count, results);
    }
}