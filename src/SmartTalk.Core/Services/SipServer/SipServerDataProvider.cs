using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.SipServer;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.SipServer;

public interface ISipServerDataProvider : IScopedDependency
{
    Task<List<SipHostServer>> GetAllSipHostServersAsync(CancellationToken cancellationToken);

    Task<List<SipBackupServer>> GetAllSipBackupServersAsync(CancellationToken cancellationToken);
    
    Task<List<SipHostServer>> AddSipHostServersAsync(List<SipHostServer> servers, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<List<SipBackupServer>> AddSipBackupServersAsync(List<SipBackupServer> servers, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateSipHostServersAsync(List<SipHostServer> servers, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdateSipBackupServersAsync(List<SipBackupServer> servers, bool forceSave = true, CancellationToken cancellationToken = default);
}

public class SipServerDataProvider : ISipServerDataProvider
{
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SipServerDataProvider(IRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }
    
    public async Task<List<SipHostServer>> GetAllSipHostServersAsync(CancellationToken cancellationToken)
    {
        var query = _repository.Query<SipHostServer>();

        var hostServers = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        var backupServers = await GetAllSipBackupServersAsync(cancellationToken).ConfigureAwait(false);
        
        hostServers.ForEach(x =>
        {
            x.BackupServers = backupServers.Where(b => b.HostId == x.Id).ToList();
        });

        return hostServers;
    }
    
    public async Task<List<SipBackupServer>> GetAllSipBackupServersAsync(CancellationToken cancellationToken)
    {
        return await _repository.Query<SipBackupServer>().ToListAsync(cancellationToken).ConfigureAwait(false);  
    }

    public async Task<List<SipHostServer>> AddSipHostServersAsync(List<SipHostServer> servers, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(servers, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return servers;
    }
    
    public async Task<List<SipBackupServer>> AddSipBackupServersAsync(List<SipBackupServer> servers, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(servers, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        
        return servers;
    }

    public async Task UpdateSipHostServersAsync(List<SipHostServer> servers, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(servers, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateSipBackupServersAsync(List<SipBackupServer> servers, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(servers, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}