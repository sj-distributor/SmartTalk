using AutoMapper;
using Serilog;
using SmartTalk.Core.Domain.Asterisk;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Domain.Linphone;
using SmartTalk.Core.Services.Caching;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Messages.Dto.Linphone;
using SmartTalk.Messages.Enums.Linphone;
using SmartTalk.Messages.Requests.Linphone;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Core.Services.Linphone;

public interface ILinphoneService : IScopedDependency
{
    Task AddLinphoneCdrAsync(string recordName, LinphoneStatus? linphoneStatus = null, CancellationToken cancellationToken = default);

    Task<GetLinphoneHistoryResponse> GetLinphoneHistoryAsync(GetLinphoneHistoryRequest request, CancellationToken cancellationToken);

    Task<GetAgentBySipResponse> GetAgentBySipAsync(GetAgentBySipRequest request, CancellationToken cancellationToken);

    Task<GetLinphoneHistoryDetailsResponse> GetLinphoneHistoryDetailsAsync(GetLinphoneHistoryDetailsRequest request, CancellationToken cancellationToken);
    
    Task AutoGetLinphoneCdrRecordAsync(CancellationToken cancellationToken);

    Task<GetLinphoneRestaurantNumberResponse> GetLinphoneRestaurantNumberAsync(GetLinphoneRestaurantNumberRequest request, CancellationToken cancellationToken);

    Task<GetLinphoneDataResponse> GetLinphoneDataAsync(GetLinphoneDataRequest request, CancellationToken cancellationToken);
}

public class LinphoneService : ILinphoneService
{
    private readonly IMapper _mapper;
    private readonly ICacheManager _cacheManager;
    private readonly IAsteriskClient _asteriskClient;
    private readonly ILinphoneDataProvider _linphoneDataProvider;

    public LinphoneService(IMapper mapper, ICacheManager cacheManager, IAsteriskClient asteriskClient, ILinphoneDataProvider linphoneDataProvider)
    {
        _mapper = mapper;
        _cacheManager = cacheManager;
        _asteriskClient = asteriskClient;
        _linphoneDataProvider = linphoneDataProvider;
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
    
    public async Task<LinphoneCdr> AddLinphoneCdrsAsync(string recordName, List<LinphoneCdrDto> cdrs, List<LinphoneSip> linphoneSips, LinphoneStatus? linphoneStatus = null, CancellationToken cancellationToken = default)
    {
        Log.Information($"Add cdr record parameter: {recordName}", recordName);
        
        Log.Information("LinphoneSips: {@linphoneSips}", linphoneSips);
        
        var parts = recordName.Split('.')[0].Split('-');
    
        if (parts.Length < 6) return null;
    
        var callType = parts[0];
        var recipient = parts[1];
        var caller = parts[2];
    
        if ((callType != "in" && callType != "out" && callType != "rg") || !linphoneSips.Exists(x => callType switch
            {
                "in" => x.Sip == recipient,
                "rg" => cdrs.Exists(s => s.Did == x.Sip),
                "out" => x.Sip == caller
            })) return null;
    
        if (!long.TryParse(parts[5], out var callTimestamp)) return null;
    
        var agentId = linphoneSips.First(x => callType switch
        {
            "in" => x.Sip == recipient,
            "rg" => cdrs.Exists(s => s.Did == x.Sip),
            "out" => x.Sip == caller
        }).AgentId;
    
        var linphoneCdr = new LinphoneCdr
        {
            Caller = caller,
            Status = linphoneStatus ?? callType switch
            {
                "in" => LinphoneStatus.InComing,
                "rg" => LinphoneStatus.InComing,
                "out" => LinphoneStatus.OutGoing
            },
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
        
        var externalLinphoneCdrs = await _asteriskClient.GetLinphoneCdrAsync(lastTime.ToString(), cancellationToken).ConfigureAwait(false);

        Log.Information("LinphoneCdrs: {@linphoneCdrs}", externalLinphoneCdrs);

        if (externalLinphoneCdrs == null) return;

        var redisKey = $"cdr-{lastTime.ToString()}";
        
        Log.Information("rediskey:{@redisKey}", redisKey);
        
        var externalLinphoneCdrsLast = await _cacheManager.GetAsync<List<LinphoneCdrDto>>(redisKey, new RedisCachingSetting(), cancellationToken).ConfigureAwait(false);
        
        Log.Information("LinphoneCdrs cache: {@linphoneCdrs}", externalLinphoneCdrsLast);

        if (externalLinphoneCdrsLast is { Count: > 0 })
            externalLinphoneCdrs.Cdrs = externalLinphoneCdrs.Cdrs.Where(x => !externalLinphoneCdrsLast.Select(y => y.Uniqueid).Contains(x.Uniqueid)).ToList();
        
        await _linphoneDataProvider.AddCdrAsync(_mapper.Map<List<Cdr>>(externalLinphoneCdrs.Cdrs), cancellationToken: cancellationToken).ConfigureAwait(false);

        if (externalLinphoneCdrs.Cdrs.Count > 0)
            await _cacheManager.SetAsync(redisKey, externalLinphoneCdrs.Cdrs, new RedisCachingSetting(expiry: TimeSpan.FromHours(1)), cancellationToken);
        
        var linphoneSips = await _linphoneDataProvider.GetLinphoneSipAsync(cancellationToken).ConfigureAwait(false);

        var newLinphoneCdrs = new List<LinphoneCdr>();

        externalLinphoneCdrs.Cdrs = externalLinphoneCdrs.Cdrs.Where(x => !string.IsNullOrEmpty(x.RecordingFile)).ToList();
        
        var externalLinphoneGroupedCdrs = externalLinphoneCdrs.Cdrs.GroupBy(x => x.RecordingFile);

        var tasks = externalLinphoneGroupedCdrs.Select(async group =>
        {
            Log.Information("LinphoneCdr: {@group}", group);

            LinphoneCdr linphoneCdr;
            
            if (group.Any(s => s.Disposition == "ANSWERED"))
                linphoneCdr = await AddLinphoneCdrsAsync(group.Key, group.ToList(), linphoneSips, cancellationToken: cancellationToken);
            else
                linphoneCdr = await AddLinphoneCdrsAsync(group.Key, group.ToList(), linphoneSips, LinphoneStatus.Missed, cancellationToken);

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

    public async Task<GetLinphoneRestaurantNumberResponse> GetLinphoneRestaurantNumberAsync(
        GetLinphoneRestaurantNumberRequest request, CancellationToken cancellationToken)
    {
        var restaurant = await _linphoneDataProvider.GetRestaurantPhoneNumberAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var restaurantPhoneNumbers = restaurant.Where(x => x.AnotherName != null).FirstOrDefault(x => request.ToRestaurant.Contains(x.AnotherName));
        
        return new GetLinphoneRestaurantNumberResponse
        {
            Data = restaurantPhoneNumbers?.PhoneNumber
        };
    }

    public async Task<GetLinphoneDataResponse> GetLinphoneDataAsync(GetLinphoneDataRequest request, CancellationToken cancellationToken)
    {
        var specifiedDate = request.Time.Date;
        var pstOffset = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles").GetUtcOffset(specifiedDate);
        var startPst = new DateTimeOffset(specifiedDate, pstOffset);
        var endPst = startPst.AddDays(1);

        Log.Information("Filter time: start: {@startPst} end: {@endPst}", startPst, endPst);
        
        try
        {
            var restaurants = await _linphoneDataProvider.GetRestaurantSipAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            
            var linphoneCdrs = await _linphoneDataProvider.GetCdrsAsync(startPst.ToUnixTimeSeconds(), endPst.ToUnixTimeSeconds(), cancellationToken).ConfigureAwait(false);
           
            linphoneCdrs = linphoneCdrs.Where(x => !string.IsNullOrEmpty(x.RecordingFile)).ToList();
            
            Log.Information("LinphoneCdrs: {@linphoneCdrs}", linphoneCdrs);
            
            var externalLinphoneGroupedCdrs = linphoneCdrs.GroupBy(x => x.RecordingFile);

            var linphoneDates = new List<LinphoneData>();
            
            var pacificZone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
            
            var tasks = externalLinphoneGroupedCdrs.Select(async group =>
            {
                Log.Information("Cdr: {@group}", group);

                LinphoneData linphoneData;

                if (group.Any(s => s.Disposition == "ANSWERED"))
                {
                    var answeredCdr = group.First(x => x.Disposition == "ANSWERED"); 
                    
                    linphoneData = new LinphoneData
                    {
                        CallId = answeredCdr.Id,
                        MerchName = restaurants.FirstOrDefault(x => x.Key == answeredCdr.Did).Value ?? restaurants.FirstOrDefault(x => x.Key == answeredCdr.Cnum).Value ?? "未知",
                        CallTime = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds((long)answeredCdr.Uniqueid), pacificZone).ToString("yyyy-MM-dd HH:mm:ss"),
                        PickStat = "On",
                        CallPeriod = TimeSpan.FromSeconds(int.Parse(answeredCdr.Duration)).ToString(@"mm\:ss")
                    };
                }
                else
                {
                    var answeredCdr = group.First(); 
                    
                    linphoneData = new LinphoneData
                    {
                        CallId = answeredCdr.Id,
                        MerchName = restaurants.FirstOrDefault(x => x.Key == answeredCdr.Did).Value ?? restaurants.FirstOrDefault(x => x.Key == answeredCdr.Cnum).Value ?? "未知",
                        CallTime = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds((long)answeredCdr.Uniqueid), pacificZone).ToString("yyyy-MM-dd HH:mm:ss"),
                        PickStat = "Cut",
                        CallPeriod = TimeSpan.FromSeconds(int.Parse(answeredCdr.Duration)).ToString(@"mm\:ss")
                    };
                }

                return linphoneData;
            });

            var results = await Task.WhenAll(tasks);
            
            linphoneDates.AddRange(results);

            linphoneDates.RemoveAll(x => x == null);
            
            return new GetLinphoneDataResponse
            {
                Data = linphoneDates
            };
        }
        catch (Exception e)
        {
            Log.Error(e, "Redis occur error: {ErrorMessage}", e.Message);
        }
        
        return new GetLinphoneDataResponse();
    }
}