using AutoMapper;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Account;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.Caching;
using SmartTalk.Core.Services.Caching.Redis;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.RetrievalDb.VectorDb;
using SmartTalk.Messages.Commands.Pos;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Services.Pos;

public partial interface IPosService : IScopedDependency
{
    Task<GetPosCompanyWithStoresResponse> GetPosCompanyWithStoresAsync(GetPosCompanyWithStoresRequest request, CancellationToken cancellationToken);
    
    Task<GetPosCompanyStoreDetailResponse> GetPosCompanyStoreDetailAsync(GetPosCompanyStoreDetailRequest request, CancellationToken cancellationToken);
    
    Task<CreatePosCompanyStoreResponse> CreatePosCompanyStoreAsync(CreatePosCompanyStoreCommand command,CancellationToken cancellationToken);
    
    Task<UpdatePosCompanyStoreResponse> UpdatePosCompanyStoreAsync(UpdatePosCompanyStoreCommand command,CancellationToken cancellationToken);
    
    Task<DeletePosCompanyStoreResponse> DeletePosCompanyStoreAsync(DeletePosCompanyStoreCommand command,CancellationToken cancellationToken);
    
    Task<UpdatePosCompanyStoreStatusResponse> UpdatePosCompanyStoreStatusAsync(UpdatePosCompanyStoreStatusCommand command,CancellationToken cancellationToken);

    Task<ManagePosCompanyStoreAccountsResponse> ManagePosCompanyStoreAccountAsync(ManagePosCompanyStoreAccountsCommand command, CancellationToken cancellationToken);

    Task<GetPosStoreUsersResponse> GetPosStoreUsersAsync(GetPosStoreUsersRequest request, CancellationToken cancellationToken);

    Task<UnbindPosCompanyStoreResponse> UnbindPosCompanyStoreAsync(UnbindPosCompanyStoreCommand command, CancellationToken cancellationToken);

    Task<BindPosCompanyStoreResponse> BindPosCompanyStoreAsync(BindPosCompanyStoreCommand command, CancellationToken cancellationToken);
    
    Task<GetPosStoresResponse> GetPosStoresAsync(GetPosStoresRequest request, CancellationToken cancellationToken);
}

public partial class PosService : IPosService
{
    private readonly IMapper _mapper;
    private readonly IVectorDb _vectorDb;
    private readonly ICurrentUser _currentUser;
    private readonly ICacheManager _cacheManager;
    private readonly IEasyPosClient _easyPosClient;
    private readonly ISmartiesClient _smartiesClient;
    private readonly IRedisSafeRunner _redisSafeRunner;
    private readonly IPosDataProvider _posDataProvider;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IAccountDataProvider _accountDataProvider;
    private readonly ISmartTalkBackgroundJobClient _smartTalkBackgroundJobClient;
    
    public PosService(
        IMapper mapper,
        IVectorDb vectorDb,
        ICurrentUser currentUser,
        ICacheManager cacheManager,
        IEasyPosClient easyPosClient,
        ISmartiesClient smartiesClient,
        IRedisSafeRunner redisSafeRunner,
        IPosDataProvider posDataProvider,
        IAgentDataProvider agentDataProvider,
        IAccountDataProvider accountDataProvider,
        ISmartTalkBackgroundJobClient smartTalkBackgroundJobClient)
    {
        _mapper = mapper;
        _vectorDb = vectorDb;
        _currentUser = currentUser;
        _cacheManager = cacheManager;
        _easyPosClient = easyPosClient;
        _smartiesClient = smartiesClient;
        _redisSafeRunner = redisSafeRunner;
        _posDataProvider = posDataProvider;
        _agentDataProvider = agentDataProvider;
        _accountDataProvider = accountDataProvider;
        _smartTalkBackgroundJobClient = smartTalkBackgroundJobClient;
    }
    
    public async Task<GetPosCompanyWithStoresResponse> GetPosCompanyWithStoresAsync(GetPosCompanyWithStoresRequest request, CancellationToken cancellationToken)
    {
        var (count, companies) = await _posDataProvider.GetPosCompaniesAsync(
            request.PageIndex, request.PageSize, cancellationToken: cancellationToken).ConfigureAwait(false);

        var result = _mapper.Map<List<PosCompanyDto>>(companies);
        
        return new GetPosCompanyWithStoresResponse
        {
            Data = new GetPosCompanyWithStoresResponseData
            {
                Count = count,
                Data = await EnrichPosCompaniesAsync(result, request.Keyword, cancellationToken).ConfigureAwait(false)
            }
        };
    }

    public async Task<GetPosCompanyStoreDetailResponse> GetPosCompanyStoreDetailAsync(GetPosCompanyStoreDetailRequest request, CancellationToken cancellationToken)
    {
        var store = await _posDataProvider.GetPosCompanyStoreDetailAsync(request.StoreId, cancellationToken).ConfigureAwait(false);

        return new GetPosCompanyStoreDetailResponse
        {
            Data = store
        };
    }
    
    public async Task<CreatePosCompanyStoreResponse> CreatePosCompanyStoreAsync(CreatePosCompanyStoreCommand command, CancellationToken cancellationToken)
    {
        var store = _mapper.Map<PosCompanyStore>(command);

        store.CreatedBy = _currentUser.Id.Value;

        await _posDataProvider.AddPosCompanyStoresAsync([store], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await InitialAgentAsync(store.Id, cancellationToken).ConfigureAwait(false);
        
        await _vectorDb.CreateIndexAsync($"pos-{store.Id}", 3072, cancellationToken).ConfigureAwait(false);

        return new CreatePosCompanyStoreResponse
        {
            Data = _mapper.Map<PosCompanyStoreDto>(store)
        };
    }

    public async Task<UpdatePosCompanyStoreResponse> UpdatePosCompanyStoreAsync(UpdatePosCompanyStoreCommand command, CancellationToken cancellationToken)
    {
        var store = await _posDataProvider.GetPosCompanyStoreAsync(id: command.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        _mapper.Map(command, store);

        await _posDataProvider.UpdatePosCompanyStoresAsync([store], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new UpdatePosCompanyStoreResponse
        {
            Data = _mapper.Map<PosCompanyStoreDto>(store)
        };
    }

    public async Task<DeletePosCompanyStoreResponse> DeletePosCompanyStoreAsync(DeletePosCompanyStoreCommand command, CancellationToken cancellationToken)
    {
        var stores = await _posDataProvider.GetPosCompanyStoresAsync([command.StoreId], cancellationToken:cancellationToken).ConfigureAwait(false);

        if (stores.Count == 0) throw new Exception("Could not found any stores");

        await _posDataProvider.DeletePosCompanyStoresAsync(stores, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new DeletePosCompanyStoreResponse
        {
            Data = _mapper.Map<List<PosCompanyStoreDto>>(stores)
        };
    }

    public async Task<UpdatePosCompanyStoreStatusResponse> UpdatePosCompanyStoreStatusAsync(UpdatePosCompanyStoreStatusCommand command, CancellationToken cancellationToken)
    {
        var store = await _posDataProvider.GetPosCompanyStoreAsync(id: command.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (store == null) throw new Exception("Could not found the store");
        
        store.Status = command.Status;

        await _posDataProvider.UpdatePosCompanyStoresAsync([store], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new UpdatePosCompanyStoreStatusResponse
        {
            Data = _mapper.Map<PosCompanyStoreDto>(store)
        };
    }

    public async Task<UnbindPosCompanyStoreResponse> UnbindPosCompanyStoreAsync(UnbindPosCompanyStoreCommand command, CancellationToken cancellationToken)
    {
        var store = await _posDataProvider.GetPosCompanyStoreAsync(id: command.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (store == null) throw new Exception("Could not found the store");

        store.IsLink = false;
        store.Link = null;
        store.AppSecret = null;
        store.AppId = null;
        store.PosId = null;
        store.PosName = null;
        
        await _posDataProvider.UpdatePosCompanyStoresAsync([store], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new UnbindPosCompanyStoreResponse
        {
            Data = _mapper.Map<PosCompanyStoreDto>(store)
        };
    }

    public async Task<BindPosCompanyStoreResponse> BindPosCompanyStoreAsync(BindPosCompanyStoreCommand command, CancellationToken cancellationToken)
    {
        var store = await _posDataProvider.GetPosCompanyStoreAsync(id: command.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);

        var existingUrlStore = await _posDataProvider.GetPosCompanyStoreAsync(appId: command.AppId, appSecret: command.AppSecret, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (existingUrlStore != null) throw new Exception("The store has been bound.");

        var easyPosMerchant = await _easyPosClient.GetPosCompanyStoreMessageAsync(new EasyPosTokenRequestDto()
        {
            BaseUrl = command.Link,
            AppId = command.AppId,
            AppSecret = command.AppSecret
        }, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get the merchant info: {@merchant}", easyPosMerchant);
        
        store.Link = command.Link;
        store.AppId = command.AppId;
        store.AppSecret = command.AppSecret;
        store.IsLink = true;
        store.PosId = easyPosMerchant?.Data?.Id.ToString() ?? string.Empty;
        store.PosName = easyPosMerchant?.Data?.ShortName ?? string.Empty;
        store.Timezone = easyPosMerchant?.Data?.TimeZoneId ?? string.Empty;
        store.TimePeriod = JsonConvert.SerializeObject(easyPosMerchant?.Data?.TimePeriods);

        await _posDataProvider.UpdatePosCompanyStoresAsync([store], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new BindPosCompanyStoreResponse()
        {
            Data = _mapper.Map<PosCompanyStoreDto>(store)
        };
    }

    public async Task<ManagePosCompanyStoreAccountsResponse> ManagePosCompanyStoreAccountAsync(ManagePosCompanyStoreAccountsCommand command, CancellationToken cancellationToken)
    {
        command.UserIds ??= new List<int>();

        var existingAccounts = await _posDataProvider.GetPosStoreUsersAsync(command.StoreId, cancellationToken).ConfigureAwait(false);

        if (existingAccounts.Any())
        {
            await _posDataProvider.DeletePosStoreUsersAsync(_mapper.Map<List<PosStoreUser>>(existingAccounts), cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        List<PosStoreUser> newAccounts = new();
    
        if (command.UserIds.Any())
        {
            newAccounts = command.UserIds.Select(userId => new PosStoreUser
                {
                    UserId = userId,
                    StoreId = command.StoreId,
                    CreatedBy = _currentUser.Id!.Value,
                    CreatedDate = DateTimeOffset.UtcNow
                }).ToList();

            await _posDataProvider.CreatePosStoreUserAsync(newAccounts, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return new ManagePosCompanyStoreAccountsResponse 
        {
            Data = _mapper.Map<List<PosStoreUserDto>>(newAccounts)
        };
    }

    public async Task<GetPosStoreUsersResponse> GetPosStoreUsersAsync(GetPosStoreUsersRequest request, CancellationToken cancellationToken)
    {
        var posStoreUsers = await _posDataProvider.GetPosStoreUsersAsync(request.StoreId, cancellationToken).ConfigureAwait(false);

        if (!posStoreUsers.Any())
            return new GetPosStoreUsersResponse
            {
                Data = new List<PosStoreUserDto>()
            };

        return new GetPosStoreUsersResponse
        {
            Data = posStoreUsers
        };
    }

    public async Task<GetPosStoresResponse> GetPosStoresAsync(GetPosStoresRequest request, CancellationToken cancellationToken)
    {
        var storeUsers = request.AuthorizedFilter
            ? await _posDataProvider.GetPosStoreUsersByUserIdAsync(_currentUser.Id.Value, cancellationToken).ConfigureAwait(false)
            : null;
            
        var stores = await _posDataProvider.GetPosCompanyStoresWithSortingAsync(
            storeUsers?.Select(x => x.StoreId).ToList(),
            request.CompanyId, request.Keyword, request.IsNormalSort, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new GetPosStoresResponse
        {
            Data = _mapper.Map<List<PosCompanyStoreDto>>(stores)
        };
    }

    private async Task<List<GetPosCompanyWithStoresData>> EnrichPosCompaniesAsync(List<PosCompanyDto> companies, string keyword, CancellationToken cancellationToken)
    {
        var stores = await _posDataProvider.GetPosCompanyStoresAsync(
            companyIds: companies.Select(x => x.Id).ToList(), cancellationToken: cancellationToken).ConfigureAwait(false);

        var storeGroups = stores.GroupBy(x => x.CompanyId).ToDictionary(kvp => kvp.Key, kvp => kvp.ToList());

        var data = companies.Select(x => new GetPosCompanyWithStoresData
        {
            Company = x,
            Stores = EnrichCompanyStores(x, storeGroups),
            Count = storeGroups.TryGetValue(x.Id, out var group) ? group.Count : 0
        }).ToList();
        
        if (string.IsNullOrWhiteSpace(keyword)) return data;
        
        return data.Where(x =>
                (x.Company.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                x.Stores.Any(s => s.Names?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
    }

    private List<PosCompanyStoreDto> EnrichCompanyStores(PosCompanyDto company, Dictionary<int,List<PosCompanyStore>> storeGroups)
    {
        var stores = storeGroups.TryGetValue(company.Id, out var group) ? group : [];
        
        return _mapper.Map<List<PosCompanyStoreDto>>(stores);
    }

    private async Task InitialAgentAsync(int storeId, CancellationToken cancellationToken)
    {
        var agent = new Agent
        {
            IsDisplay = true,
            Type = AgentType.Assistant,
            SourceSystem = AgentSourceSystem.Self
        };
        
        await _agentDataProvider.AddAgentAsync(agent, cancellationToken: cancellationToken).ConfigureAwait(false);

        var posAgent = new PosAgent
        {
            StoreId = storeId,
            AgentId = agent.Id
        };
        
        await _posDataProvider.AddPosAgentsAsync([posAgent], cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}