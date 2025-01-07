using SmartTalk.Core.Domain.SipServer;
using SmartTalk.Messages.Commands.SipServer;
using SmartTalk.Messages.Dto.SipServer;

namespace SmartTalk.Core.Services.SipServer;
public partial interface ISipServerService
{
    Task<AddSipHostServersResponse> AddSipHostServersAsync(AddSipHostServersCommand command, CancellationToken cancellationToken);

    Task<AddSipBackupServersResponse> AddSipBackupServersAsync(AddSipBackupServersCommand command, CancellationToken cancellationToken);
}

public partial class SipServerService
{
    public async Task<AddSipHostServersResponse> AddSipHostServersAsync(AddSipHostServersCommand command, CancellationToken cancellationToken)
    {
        var hostServers = await _sipServerDataProvider.AddSipHostServersAsync(
            _mapper.Map<List<SipHostServer>>(command.HostServers), cancellationToken: cancellationToken).ConfigureAwait(false);

        return new AddSipHostServersResponse { Data = _mapper.Map<List<SipHostServerDto>>(hostServers) };
    }
    
    public async Task<AddSipBackupServersResponse> AddSipBackupServersAsync(AddSipBackupServersCommand command, CancellationToken cancellationToken)
    {
        var backupServers = await _sipServerDataProvider.AddSipBackupServersAsync(
            _mapper.Map<List<SipBackupServer>>(command.BackupServers), cancellationToken: cancellationToken).ConfigureAwait(false);

        return new AddSipBackupServersResponse { Data = _mapper.Map<List<SipBackupServerDto>>(backupServers) };
    }
}