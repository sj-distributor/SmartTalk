using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Printer;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.Printer;

namespace SmartTalk.Core.Services.Printer;

public interface IPrinterDataProvider : IScopedDependency
{
    Task<MerchPrinter> GetMerchPrinterByPrinterMacAsync(string printerMac, Guid token, CancellationToken cancellationToken);

    Task<List<MerchPrinterOrder>> GetMerchPrinterOrdersAsync(Guid? jobToken = null, int? agentId = null, PrintStatus? status = null,
        DateTimeOffset? endTime = null, string printerMac = null, bool isOrderByPrintDate = false, CancellationToken cancellationToken = default);

    Task UpdateMerchPrinterOrderAsync(MerchPrinterOrder merchPrinterOrder, bool forceSave = true, CancellationToken cancellationToken = default);

    Task UpdateMerchPrinterMacAsync(MerchPrinter merchPrinter, bool forceSave = true, CancellationToken cancellationToken = default);

    Task AddMerchPrinterOrderAsync(MerchPrinterOrder merchPrinterOrder, CancellationToken cancellationToken);
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

    public async Task<MerchPrinter> GetMerchPrinterByPrinterMacAsync(string printerMac, Guid token, CancellationToken cancellationToken)
    {
        return await _repository.Query<MerchPrinter>().Where(x => x.PrinterMac == printerMac && x.Token==token).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<List<MerchPrinterOrder>> GetMerchPrinterOrdersAsync(Guid? jobToken = null, int? agentId = null, PrintStatus? status = null,
        DateTimeOffset? endTime = null, string printerMac = null, bool isOrderByPrintDate = false, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<MerchPrinterOrder>();

        if (jobToken.HasValue)
            query = query.Where(x => x.Id == jobToken);

        if (agentId.HasValue)
            query = query.Where(x => x.AgentId == agentId);

        if (status.HasValue)
            query = query.Where(x => x.PrintStatus == status);

        if (endTime.HasValue)
            query = query.Where(x => x.PrintDate <= endTime);

        if (!string.IsNullOrEmpty(printerMac))
            query = query.Where(x => x.PrinterMac == printerMac);

        if (isOrderByPrintDate)
            query = query.OrderBy(x => x.PrintDate);

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
}