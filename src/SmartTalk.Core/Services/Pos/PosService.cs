using AutoMapper;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Account;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Caching;
using SmartTalk.Core.Services.Caching.Redis;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.RetrievalDb.VectorDb;
using SmartTalk.Core.Services.Security;
using SmartTalk.Messages.Commands.Pos;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Services.Pos;

public partial interface IPosService : IScopedDependency
{
    Task<GetCompanyWithStoresResponse> GetCompanyWithStoresAsync(GetCompanyWithStoresRequest request, CancellationToken cancellationToken);
    
    Task<GetCompanyStoreDetailResponse> GetCompanyStoreDetailAsync(GetCompanyStoreDetailRequest request, CancellationToken cancellationToken);
    
    Task<CreateCompanyStoreResponse> CreateCompanyStoreAsync(CreateCompanyStoreCommand command,CancellationToken cancellationToken);
    
    Task<UpdateCompanyStoreResponse> UpdateCompanyStoreAsync(UpdateCompanyStoreCommand command,CancellationToken cancellationToken);
    
    Task<DeleteCompanyStoreResponse> DeleteCompanyStoreAsync(DeleteCompanyStoreCommand command,CancellationToken cancellationToken);
    
    Task<UpdateCompanyStoreStatusResponse> UpdateCompanyStoreStatusAsync(UpdateCompanyStoreStatusCommand command,CancellationToken cancellationToken);

    Task<ManageCompanyStoreAccountsResponse> ManageCompanyStoreAccountAsync(ManageCompanyStoreAccountsCommand command, CancellationToken cancellationToken);

    Task<GetStoreUsersResponse> GetStoreUsersAsync(GetStoreUsersRequest request, CancellationToken cancellationToken);

    Task<UnbindPosCompanyStoreResponse> UnbindPosCompanyStoreAsync(UnbindPosCompanyStoreCommand command, CancellationToken cancellationToken);

    Task<BindPosCompanyStoreResponse> BindPosCompanyStoreAsync(BindPosCompanyStoreCommand command, CancellationToken cancellationToken);
    
    Task<GetPosStoresResponse> GetStoresAsync(GetStoresRequest request, CancellationToken cancellationToken);

    Task<GetCurrentUserStoresResponse> GetCurrentUserStoresAsync(GetCurrentUserStoresRequest request, CancellationToken cancellationToken);
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
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    private readonly ISecurityDataProvider _securityDataProvider;
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
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider,
        ISecurityDataProvider  securityDataProvider,
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
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _securityDataProvider = securityDataProvider;
        _smartTalkBackgroundJobClient = smartTalkBackgroundJobClient;
    }
    
    public async Task<GetCompanyWithStoresResponse> GetCompanyWithStoresAsync(GetCompanyWithStoresRequest request, CancellationToken cancellationToken)
    {
        var (count, companies) = await _posDataProvider.GetPosCompaniesAsync(
            request.PageIndex, request.PageSize, serviceProviderId: request.ServiceProviderId, cancellationToken: cancellationToken).ConfigureAwait(false);

        var result = _mapper.Map<List<CompanyDto>>(companies);
        
        return new GetCompanyWithStoresResponse
        {
            Data = new GetCompanyWithStoresResponseData
            {
                Count = count,
                Data = await EnrichPosCompaniesAsync(result, request.Keyword, cancellationToken).ConfigureAwait(false)
            }
        };
    }

    public async Task<GetCompanyStoreDetailResponse> GetCompanyStoreDetailAsync(GetCompanyStoreDetailRequest request, CancellationToken cancellationToken)
    {
        var store = await _posDataProvider.GetPosCompanyStoreDetailAsync(request.StoreId, cancellationToken).ConfigureAwait(false);

        return new GetCompanyStoreDetailResponse
        {
            Data = store
        };
    }
    
    public async Task<CreateCompanyStoreResponse> CreateCompanyStoreAsync(CreateCompanyStoreCommand command, CancellationToken cancellationToken)
    {
        var store = _mapper.Map<CompanyStore>(command);

        store.CreatedBy = _currentUser.Id.Value;

        await _posDataProvider.AddPosCompanyStoresAsync([store], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await _vectorDb.CreateIndexAsync($"pos-{store.Id}", 3072, cancellationToken).ConfigureAwait(false);

        return new CreateCompanyStoreResponse
        {
            Data = _mapper.Map<CompanyStoreDto>(store)
        };
    }

    public async Task<UpdateCompanyStoreResponse> UpdateCompanyStoreAsync(UpdateCompanyStoreCommand command, CancellationToken cancellationToken)
    {
        var store = await _posDataProvider.GetPosCompanyStoreAsync(id: command.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        _mapper.Map(command, store);

        await _posDataProvider.UpdatePosCompanyStoresAsync([store], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new UpdateCompanyStoreResponse
        {
            Data = _mapper.Map<CompanyStoreDto>(store)
        };
    }

    public async Task<DeleteCompanyStoreResponse> DeleteCompanyStoreAsync(DeleteCompanyStoreCommand command, CancellationToken cancellationToken)
    {
        var stores = await _posDataProvider.GetPosCompanyStoresAsync([command.StoreId], cancellationToken:cancellationToken).ConfigureAwait(false);

        if (stores.Count == 0) throw new Exception("Could not found any stores");

        await _posDataProvider.DeletePosCompanyStoresAsync(stores, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new DeleteCompanyStoreResponse
        {
            Data = _mapper.Map<List<CompanyStoreDto>>(stores)
        };
    }

    public async Task<UpdateCompanyStoreStatusResponse> UpdateCompanyStoreStatusAsync(UpdateCompanyStoreStatusCommand command, CancellationToken cancellationToken)
    {
        var store = await _posDataProvider.GetPosCompanyStoreAsync(id: command.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (store == null) throw new Exception("Could not found the store");
        
        store.Status = command.Status;

        await _posDataProvider.UpdatePosCompanyStoresAsync([store], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new UpdateCompanyStoreStatusResponse
        {
            Data = _mapper.Map<CompanyStoreDto>(store)
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
        store.TimePeriod = null;
        
        await _posDataProvider.UpdatePosCompanyStoresAsync([store], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new UnbindPosCompanyStoreResponse
        {
            Data = _mapper.Map<CompanyStoreDto>(store)
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
        
        var timePeriods = easyPosMerchant?.Data?.TimePeriods;
        
        store.Link = command.Link;
        store.AppId = command.AppId;
        store.AppSecret = command.AppSecret;
        store.IsLink = true;
        store.PosId = easyPosMerchant?.Data?.Id.ToString() ?? string.Empty;
        store.PosName = easyPosMerchant?.Data?.ShortName ?? string.Empty;
        store.Timezone = easyPosMerchant?.Data?.TimeZoneId ?? string.Empty;
        store.TimePeriod = timePeriods != null && timePeriods.Count != 0 ? JsonConvert.SerializeObject(timePeriods) : string.Empty;

        await _posDataProvider.UpdatePosCompanyStoresAsync([store], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new BindPosCompanyStoreResponse
        {
            Data = _mapper.Map<CompanyStoreDto>(store)
        };
    }

    public async Task<ManageCompanyStoreAccountsResponse> ManageCompanyStoreAccountAsync(ManageCompanyStoreAccountsCommand command, CancellationToken cancellationToken)
    {
        command.UserIds ??= new List<int>();

        var existingAccounts = await _posDataProvider.GetPosStoreUsersAsync(command.StoreId, cancellationToken).ConfigureAwait(false);

        if (existingAccounts.Any())
        {
            await _posDataProvider.DeletePosStoreUsersAsync(_mapper.Map<List<StoreUser>>(existingAccounts), cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        List<StoreUser> newAccounts = new();
    
        if (command.UserIds.Any())
        {
            newAccounts = command.UserIds.Select(userId => new StoreUser
                {
                    UserId = userId,
                    StoreId = command.StoreId,
                    CreatedBy = _currentUser.Id!.Value,
                    CreatedDate = DateTimeOffset.UtcNow
                }).ToList();

            await _posDataProvider.CreatePosStoreUserAsync(newAccounts, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return new ManageCompanyStoreAccountsResponse 
        {
            Data = _mapper.Map<List<StoreUserDto>>(newAccounts)
        };
    }

    public async Task<GetStoreUsersResponse> GetStoreUsersAsync(GetStoreUsersRequest request, CancellationToken cancellationToken)
    {
        var posStoreUsers = await _posDataProvider.GetPosStoreUsersAsync(request.StoreId, cancellationToken).ConfigureAwait(false);

        if (!posStoreUsers.Any())
            return new GetStoreUsersResponse
            {
                Data = new List<StoreUserDto>()
            };

        return new GetStoreUsersResponse
        {
            Data = posStoreUsers
        };
    }

    public async Task<GetPosStoresResponse> GetStoresAsync(GetStoresRequest request, CancellationToken cancellationToken)
    {
        var isSuperAdmin = await CheckCurrentIsAdminAsync(cancellationToken).ConfigureAwait(false);
        
        Log.Information("The current user: {CurrrentUser} is Admin: {IsSuperAdmin}", _currentUser, isSuperAdmin);

        var storeUsers = isSuperAdmin ? null
            : request.AuthorizedFilter ? await _posDataProvider.GetPosStoreUsersByUserIdAsync(_currentUser.Id.Value, cancellationToken).ConfigureAwait(false)
            : [];
        
        Log.Information("Get store users: {StoreUsers}", storeUsers);
        
        var stores = await _posDataProvider.GetPosCompanyStoresWithSortingAsync(
            storeUsers?.Select(x => x.StoreId).ToList(),
            request.CompanyId, request.ServiceProviderId, request.Keyword, request.IsNormalSort, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new GetPosStoresResponse
        {
            Data = _mapper.Map<List<CompanyStoreDto>>(stores)
        };
    }
    
    public async Task<bool> CheckCurrentIsAdminAsync(CancellationToken cancellationToken)
    {
        var roleUsers = await _accountDataProvider.GetRoleUserByRoleAccountLevelAsync(UserAccountLevel.ServiceProvider, cancellationToken).ConfigureAwait(false);

        Log.Information("Get admin role users: {@roleUsers} by current user: {@currentUserId}", roleUsers, _currentUser.Id.Value);
        
        return roleUsers.Any(x => x.UserId == _currentUser.Id.Value);
    }

    public async Task<GetCurrentUserStoresResponse> GetCurrentUserStoresAsync(GetCurrentUserStoresRequest request, CancellationToken cancellationToken)
    {
        var storeUsers = await _posDataProvider.GetPosStoreUsersByUserIdAsync(_currentUser.Id.Value, cancellationToken).ConfigureAwait(false);

        if (storeUsers.Count == 0) return new GetCurrentUserStoresResponse { Data = [] };
        
        var storeIds = storeUsers.Select(x => x.StoreId).ToList();
        var stores = _mapper.Map<List<CompanyStoreDto>>(
            await _posDataProvider.GetPosCompanyStoresAsync(ids: storeIds, cancellationToken: cancellationToken).ConfigureAwait(false));
        
        var allAgents = await _posDataProvider.GetPosAgentsAsync(storeIds: storeIds, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var enrichStores = stores.Select(store => new GetCurrentUserStoresResponseData
        {
            Store = store,
            AgentIds = allAgents.Where(x => x.StoreId == store.Id).Select(x => x.AgentId).ToList()
        }).ToList();

        return new GetCurrentUserStoresResponse { Data = enrichStores };
    }

    private async Task<List<GetCompanyWithStoresData>> EnrichPosCompaniesAsync(List<CompanyDto> companies, string keyword, CancellationToken cancellationToken)
    {
        var stores = await _posDataProvider.GetPosCompanyStoresAsync(
            companyIds: companies.Select(x => x.Id).ToList(), cancellationToken: cancellationToken).ConfigureAwait(false);

        var storeGroups = stores.GroupBy(x => x.CompanyId).ToDictionary(kvp => kvp.Key, kvp => kvp.ToList());

        var data = companies.Select(x => new GetCompanyWithStoresData
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

    private List<CompanyStoreDto> EnrichCompanyStores(CompanyDto company, Dictionary<int,List<CompanyStore>> storeGroups)
    {
        var stores = storeGroups.TryGetValue(company.Id, out var group) ? group : [];
        
        return _mapper.Map<List<CompanyStoreDto>>(stores);
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