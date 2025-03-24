using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Domain.Linphone;
using SmartTalk.Messages.Enums.Linphone;
using SmartTalk.Messages.Requests.Linphone;

namespace SmartTalk.Core.Services.Linphone;

public interface ILinphoneService : IScopedDependency
{
    Task AddLinphoneCdrAsync(string recordName, CancellationToken cancellationToken);

    Task<GetLinphoneHistoryResponse> GetLinphoneHistoryAsync(GetLinphoneHistoryRequest request, CancellationToken cancellationToken);

    Task<GetAgentBySipResponse> GetAgentBySipAsync(GetAgentBySipRequest request, CancellationToken cancellationToken);

    Task<GetLinphoneHistoryDetailsResponse> GetLinphoneHistoryDetailsAsync(GetLinphoneHistoryDetailsRequest request, CancellationToken cancellationToken);
}

public class LinphoneService : ILinphoneService
{
    private readonly ILinphoneDataProvider _linphoneDataProvider;

    public LinphoneService(ILinphoneDataProvider linphoneDataProvider)
    {
        _linphoneDataProvider = linphoneDataProvider;
    }

    public async Task AddLinphoneCdrAsync(string recordName, CancellationToken cancellationToken)
    {
        Log.Information($"Add cdr record parameter: {recordName}", recordName);
        
        var linphoneSips = await _linphoneDataProvider.GetLinphoneSipAsync(cancellationToken).ConfigureAwait(false);
        
        Log.Information("LinphoneSips: {@linphoneSips}", linphoneSips);
        
        var parts = recordName.Split('.')[0].Split('-');
    
        if (parts.Length < 6) return;
    
        var callType = parts[0];
        var recipient = parts[1];
        var caller = parts[2];
    
        if ((callType != "in" && callType != "out") || !linphoneSips.Exists(x => callType == "in" ? x.Sip == recipient : x.Sip == caller)) return;
    
        if (!long.TryParse(parts[5], out var callTimestamp)) return;
    
        var agentId = linphoneSips.First(x => callType == "in" ? x.Sip == recipient : x.Sip == caller).AgentId;
        var status = callType == "in" ? LinphoneStatus.InComing : LinphoneStatus.OutGoing;
    
        var linphoneCdr = new LinphoneCdr
        {
            Caller = caller,
            Status = status,
            AgentId = agentId,
            Targetter = recipient,
            CallDate = callTimestamp
        };
        
        Log.Information("Add cdr record: {@linphoneCdr}", linphoneCdr);
        
        await _linphoneDataProvider.AddLinphoneCdrAsync([linphoneCdr], cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<GetLinphoneHistoryResponse> GetLinphoneHistoryAsync(GetLinphoneHistoryRequest request, CancellationToken cancellationToken)
    {
        return new GetLinphoneHistoryResponse
        {
            Data = await _linphoneDataProvider.GetLinphoneHistoryAsync(request.AgentId, cancellationToken: cancellationToken).ConfigureAwait(false)
        };
    }

    public async Task<GetAgentBySipResponse> GetAgentBySipAsync(GetAgentBySipRequest request, CancellationToken cancellationToken)
    {
        return new GetAgentBySipResponse
        {
            Data = await _linphoneDataProvider.GetAgentBySipAsync(request.Sips, cancellationToken).ConfigureAwait(false)
        };
    }

    public async Task<GetLinphoneHistoryDetailsResponse> GetLinphoneHistoryDetailsAsync(
        GetLinphoneHistoryDetailsRequest request, CancellationToken cancellationToken)
    {
        return new GetLinphoneHistoryDetailsResponse
        {
            Data = await _linphoneDataProvider.GetLinphoneHistoryAsync(targgeter: request.Targetter, cancellationToken: cancellationToken).ConfigureAwait(false)
        };
    }
}