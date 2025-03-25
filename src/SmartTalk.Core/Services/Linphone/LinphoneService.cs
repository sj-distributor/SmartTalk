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
        var agentIds = string.IsNullOrEmpty(request.AgentId) ? [] : request.AgentId.Split(',').Select(int.Parse).ToList();
        var status = string.IsNullOrEmpty(request.Status) ? [] : request.Status.Split(',').Select(Enum.Parse<LinphoneStatus>).ToList();
        
        var (count, cdrs) = await _linphoneDataProvider.GetLinphoneHistoryAsync(
            agentIds, restaurantName: request.RestaurantName, status: status, pageSize: request.PageSize, pageIndex: request.PageIndex, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return new GetLinphoneHistoryResponse
        {
            Data = new GetLinphoneHistoryDto
            {
                linphoneRecords = cdrs,
                Count = count
            }
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
            Data = (await _linphoneDataProvider.GetLinphoneHistoryAsync(caller: request.Caller, cancellationToken: cancellationToken).ConfigureAwait(false)).Item2
        };
    }
}