using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Domain.VoiceAi.PosManagement;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Account;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Requests.VoiceAi.PosManagement;

namespace SmartTalk.Core.Services.VoiceAi.PosManagement;

public partial interface IPosManagementService : IScopedDependency
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

    Task<GetCompanyStorePosResponse> GetCompanyStorePosAsync(GetCompanyStorePosRequest request, CancellationToken cancellationToken);

    Task<BindPosCompanyStoreResponse> BindPosCompanyStoreAsync(BindPosCompanyStoreCommand command, CancellationToken cancellationToken);
}

public partial class PosManagementService : IPosManagementService
{
    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IEasyPosClient _easyPosClient;
    private readonly IPosManagementDataProvider _posManagementDataProvider;
    private readonly IAccountDataProvider _accountDataProvider;
    
    public PosManagementService(IMapper mapper, ICurrentUser currentUser, IEasyPosClient easyPosClient, IPosManagementDataProvider posManagementDataProvider, IAccountDataProvider accountDataProvider)
    {
        _mapper = mapper;
        _currentUser = currentUser;
        _easyPosClient = easyPosClient;
        _posManagementDataProvider = posManagementDataProvider;
        _accountDataProvider = accountDataProvider;
    }
    
    public async Task<GetPosCompanyWithStoresResponse> GetPosCompanyWithStoresAsync(GetPosCompanyWithStoresRequest request, CancellationToken cancellationToken)
    {
        var (count, companies) = await _posManagementDataProvider.GetPosCompaniesAsync(
            request.PageIndex, request.PageSize, keyword: request.Keyword, cancellationToken: cancellationToken).ConfigureAwait(false);

        var result = _mapper.Map<List<PosCompanyDto>>(companies);
        
        return new GetPosCompanyWithStoresResponse
        {
            Data = new GetPosCompanyWithStoresResponseData
            {
                Count = count,
                Data = await EnrichPosCompaniesAsync(result, cancellationToken).ConfigureAwait(false)
            }
        };
    }

    public async Task<GetPosCompanyStoreDetailResponse> GetPosCompanyStoreDetailAsync(GetPosCompanyStoreDetailRequest request, CancellationToken cancellationToken)
    {
        var store = await _posManagementDataProvider.GetPosCompanyStoreDetailAsync(request.StoreId, cancellationToken).ConfigureAwait(false);

        return new GetPosCompanyStoreDetailResponse
        {
            Data = store
        };
    }
    
    public async Task<CreatePosCompanyStoreResponse> CreatePosCompanyStoreAsync(CreatePosCompanyStoreCommand command, CancellationToken cancellationToken)
    {
        var store = _mapper.Map<PosCompanyStore>(command);

        store.CreatedBy = _currentUser.Id.Value;

        await _posManagementDataProvider.AddPosCompanyStoresAsync([store], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new CreatePosCompanyStoreResponse
        {
            Data = _mapper.Map<PosCompanyStoreDto>(store)
        };
    }

    public async Task<UpdatePosCompanyStoreResponse> UpdatePosCompanyStoreAsync(UpdatePosCompanyStoreCommand command, CancellationToken cancellationToken)
    {
        var store = await _posManagementDataProvider.GetPosCompanyStoreAsync(id: command.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        _mapper.Map(command, store);

        await _posManagementDataProvider.UpdatePosCompanyStoresAsync([store], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new UpdatePosCompanyStoreResponse
        {
            Data = _mapper.Map<PosCompanyStoreDto>(store)
        };
    }

    public async Task<DeletePosCompanyStoreResponse> DeletePosCompanyStoreAsync(DeletePosCompanyStoreCommand command, CancellationToken cancellationToken)
    {
        var stores = await _posManagementDataProvider.GetPosCompanyStoresAsync([command.StoreId], cancellationToken:cancellationToken).ConfigureAwait(false);

        if (stores.Count == 0) throw new Exception("Could not found any stores");

        await _posManagementDataProvider.DeletePosCompanyStoresAsync(stores, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new DeletePosCompanyStoreResponse
        {
            Data = _mapper.Map<List<PosCompanyStoreDto>>(stores)
        };
    }

    public async Task<UpdatePosCompanyStoreStatusResponse> UpdatePosCompanyStoreStatusAsync(UpdatePosCompanyStoreStatusCommand command, CancellationToken cancellationToken)
    {
        var store = await _posManagementDataProvider.GetPosCompanyStoreAsync(id: command.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (store == null) throw new Exception("Could not found the store");
        
        store.Status = command.Status;

        await _posManagementDataProvider.UpdatePosCompanyStoresAsync([store], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new UpdatePosCompanyStoreStatusResponse
        {
            Data = _mapper.Map<PosCompanyStoreDto>(store)
        };
    }

    public async Task<UnbindPosCompanyStoreResponse> UnbindPosCompanyStoreAsync(UnbindPosCompanyStoreCommand command, CancellationToken cancellationToken)
    {
        var store = await _posManagementDataProvider.GetPosCompanyStoreAsync(id: command.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (store == null) throw new Exception("Could not found the store");

        store.IsLink = false;
        store.Link = null;
        store.AppSecret = null;
        store.AppId = null;
        store.PosId = null;
        store.PosName = null;
        
        await _posManagementDataProvider.UpdatePosCompanyStoresAsync([store], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new UnbindPosCompanyStoreResponse
        {
            Data = _mapper.Map<PosCompanyStoreDto>(store)
        };
    }

    public async Task<GetCompanyStorePosResponse> GetCompanyStorePosAsync(GetCompanyStorePosRequest request, CancellationToken cancellationToken)
    {
        var store = await _posManagementDataProvider.GetPosCompanyStoreAsync(id: request.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (store.IsLink)
        {
            store.AppSecret = store.PosName;
            store.Link = store.PosId;
        }

        return new GetCompanyStorePosResponse()
        {
            Data = _mapper.Map<PosCompanyStoreDto>(store)
        };
    }

    public async Task<BindPosCompanyStoreResponse> BindPosCompanyStoreAsync(BindPosCompanyStoreCommand command, CancellationToken cancellationToken)
    {
        var store = await _posManagementDataProvider.GetPosCompanyStoreAsync(id: command.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);

        var existingUrlStore = await _posManagementDataProvider.GetPosCompanyStoreAsync(link: command.Link, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (existingUrlStore != null) throw new Exception("PosUrl is currently bound to the store, please enter another posUrl and try again");

        var EasyPosMerchant = await _easyPosClient.GetPosCompanyStoreMessageAsync(new EasyPosTokenRequestDto()
        {
            AppId = command.AppId,
            AppSecret = command.AppSecret
        }, cancellationToken).ConfigureAwait(false);
        
        store.Link = command.Link;
        store.AppId = command.AppId;
        store.AppSecret = command.AppSecret;
        store.IsLink = true;
        store.PosId = EasyPosMerchant.Data.Id.ToString();
        store.PosName = EasyPosMerchant.Data.ShortName;

        await _posManagementDataProvider.UpdatePosCompanyStoresAsync([store], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new BindPosCompanyStoreResponse()
        {
            Data = _mapper.Map<PosCompanyStoreDto>(store)
        };
    }

    public async Task<ManagePosCompanyStoreAccountsResponse> ManagePosCompanyStoreAccountAsync(ManagePosCompanyStoreAccountsCommand command, CancellationToken cancellationToken)
    {
        command.UserIds ??= new List<int>();

        var existingAccounts = await _posManagementDataProvider.GetPosStoreUsersAsync(command.StoreId, cancellationToken).ConfigureAwait(false);

        if (existingAccounts.Any())
        {
            await _posManagementDataProvider.DeletePosStoreUsersAsync(_mapper.Map<List<PosStoreUser>>(existingAccounts), cancellationToken: cancellationToken).ConfigureAwait(false);
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

            await _posManagementDataProvider.CreatePosStoreUserAsync(newAccounts, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return new ManagePosCompanyStoreAccountsResponse 
        {
            Data = _mapper.Map<List<PosStoreUserDto>>(newAccounts)
        };
    }

    public async Task<GetPosStoreUsersResponse> GetPosStoreUsersAsync(GetPosStoreUsersRequest request, CancellationToken cancellationToken)
    {
        var posStoreUsers = await _posManagementDataProvider.GetPosStoreUsersAsync(request.StoreId, cancellationToken).ConfigureAwait(false);

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

    private async Task<List<GetPosCompanyWithStoresData>> EnrichPosCompaniesAsync(List<PosCompanyDto> companies, CancellationToken cancellationToken)
    {
        var stores = await _posManagementDataProvider.GetPosCompanyStoresAsync(
            companyIds: companies.Select(x => x.Id).ToList(), cancellationToken: cancellationToken).ConfigureAwait(false);

        var storeGroups = stores.GroupBy(x => x.CompanyId).ToDictionary(kvp => kvp.Key, kvp => kvp.ToList());

        return companies.Select(x => new GetPosCompanyWithStoresData
        {
            Company = x,
            Stores = EnrichCompanyStores(x, storeGroups),
            Count = storeGroups.TryGetValue(x.Id, out var group) ? group.Count : 0
        }).ToList();
    }

    private List<PosCompanyStoreDto> EnrichCompanyStores(PosCompanyDto company, Dictionary<int,List<PosCompanyStore>> storeGroups)
    {
        var stores = storeGroups.TryGetValue(company.Id, out var group) ? group : [];
        
        return _mapper.Map<List<PosCompanyStoreDto>>(stores);
    }
}