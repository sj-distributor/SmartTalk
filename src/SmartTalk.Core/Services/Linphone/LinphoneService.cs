using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Domain.Linphone;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Services.Communication.Twilio;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Messages.Enums.Linphone;
using SmartTalk.Messages.Requests.Linphone;

namespace SmartTalk.Core.Services.Linphone;

public interface ILinphoneService : IScopedDependency
{
    Task AddLinphoneCdrAsync(string recordName, LinphoneStatus? linphoneStatus = null, CancellationToken cancellationToken = default);

    Task<GetLinphoneHistoryResponse> GetLinphoneHistoryAsync(GetLinphoneHistoryRequest request, CancellationToken cancellationToken);

    Task<GetAgentBySipResponse> GetAgentBySipAsync(GetAgentBySipRequest request, CancellationToken cancellationToken);

    Task<GetLinphoneHistoryDetailsResponse> GetLinphoneHistoryDetailsAsync(GetLinphoneHistoryDetailsRequest request, CancellationToken cancellationToken);
    
    Task AutoGetLinphoneCdrRecordAsync(CancellationToken cancellationToken);
}

public class LinphoneService : ILinphoneService
{
    private readonly IAsteriskClient _asteriskClient;
    private readonly ILinphoneDataProvider _linphoneDataProvider;
    private readonly ITwilioServiceDataProvider _twilioServiceDataProvider;

    public LinphoneService(IAsteriskClient asteriskClient, ILinphoneDataProvider linphoneDataProvider, ITwilioServiceDataProvider twilioServiceDataProvider)
    {
        _asteriskClient = asteriskClient;
        _linphoneDataProvider = linphoneDataProvider;
        _twilioServiceDataProvider = twilioServiceDataProvider;
    }

    public async Task AddLinphoneCdrAsync(string recordName, LinphoneStatus? linphoneStatus = null, CancellationToken cancellationToken = default)
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
    
        var linphoneCdr = new LinphoneCdr
        {
            Caller = caller,
            Status = linphoneStatus ?? (callType == "in" ? LinphoneStatus.InComing : LinphoneStatus.OutGoing),
            AgentId = agentId,
            Targetter = recipient,
            CallDate = callTimestamp
        };
        
        Log.Information("Add cdr record: {@linphoneCdr}", linphoneCdr);
        
        await _linphoneDataProvider.AddLinphoneCdrAsync([linphoneCdr], cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<LinphoneCdr> AddLinphoneCdrsAsync(string recordName, List<LinphoneSip> linphoneSips, LinphoneStatus? linphoneStatus = null, CancellationToken cancellationToken = default)
    {
        Log.Information($"Add cdr record parameter: {recordName}", recordName);
        
        Log.Information("LinphoneSips: {@linphoneSips}", linphoneSips);
        
        var parts = recordName.Split('.')[0].Split('-');
    
        if (parts.Length < 6) return null;
    
        var callType = parts[0];
        var recipient = parts[1];
        var caller = parts[2];
    
        if ((callType != "in" && callType != "out") || !linphoneSips.Exists(x => callType == "in" ? x.Sip == recipient : x.Sip == caller)) return null;
    
        if (!long.TryParse(parts[5], out var callTimestamp)) return null;
    
        var agentId = linphoneSips.First(x => callType == "in" ? x.Sip == recipient : x.Sip == caller).AgentId;
    
        var linphoneCdr = new LinphoneCdr
        {
            Caller = caller,
            Status = linphoneStatus ?? (callType == "in" ? LinphoneStatus.InComing : LinphoneStatus.OutGoing),
            AgentId = agentId,
            Targetter = recipient,
            CallDate = callTimestamp
        };
        
        Log.Information("Add cdr record: {@linphoneCdr}", linphoneCdr);

        return linphoneCdr;
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

    public async Task AutoGetLinphoneCdrRecordAsync(CancellationToken cancellationToken) 
    {
        Log.Information("Start auto get linphone cdr record");
        
        var localLinphoneCdrs = await _linphoneDataProvider.GetLinphoneCdrAsync(cancellationToken).ConfigureAwait(false);

        var lastTime = localLinphoneCdrs?.CallDate ?? DateTimeOffset.Now.AddMinutes(-1).ToUnixTimeSeconds();
        
        var externalLinphoneCdrs =  await _asteriskClient.GetLinphoneCdrAsync(lastTime.ToString(), cancellationToken).ConfigureAwait(false);

        Log.Information("LinphoneCdrs: {@linphoneCdrs}", externalLinphoneCdrs);
        
        var linphoneSips = await _linphoneDataProvider.GetLinphoneSipAsync(cancellationToken).ConfigureAwait(false);

        var newLinphoneCdrs = new List<LinphoneCdr>();
        
        var externalLinphoneGroupedCdrs = externalLinphoneCdrs.Cdrs.GroupBy(x => x.RecordingFile);

        var tasks = externalLinphoneGroupedCdrs.Select(async group =>
        {
            Log.Information("LinphoneCdr: {@group}", group);

            LinphoneCdr linphoneCdr;
            
            if (group.Any(s => s.Disposition == "ANSWERED"))
                linphoneCdr = await AddLinphoneCdrsAsync(group.Key, linphoneSips, cancellationToken: cancellationToken);
            else
                linphoneCdr = await AddLinphoneCdrsAsync(group.Key, linphoneSips, LinphoneStatus.Missed, cancellationToken);

            return linphoneCdr;
        });

        var results = await Task.WhenAll(tasks);
        
        newLinphoneCdrs.AddRange(results);

        newLinphoneCdrs.RemoveAll(x => x == null);

        var alreadyExistsCdr = newLinphoneCdrs.FirstOrDefault(x => x.Caller == localLinphoneCdrs.Caller && x.Targetter == localLinphoneCdrs.Targetter && x.CallDate == localLinphoneCdrs.CallDate);
        
        if (alreadyExistsCdr != null)
        {
            if (alreadyExistsCdr.Status == localLinphoneCdrs.Status)
                newLinphoneCdrs.Remove(alreadyExistsCdr);
            else
            {
                localLinphoneCdrs.Status = alreadyExistsCdr.Status;
                
                await _linphoneDataProvider.UpdateLinphoneCdrAsync(localLinphoneCdrs, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        if (newLinphoneCdrs.Count > 0)
            await _linphoneDataProvider.AddLinphoneCdrAsync(newLinphoneCdrs, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}