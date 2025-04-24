using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Domain.VoiceAi.PosManagement;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Requests.VoiceAi.PosManagement;

namespace SmartTalk.Core.Services.VoiceAi.PosManagement;

public interface IPosManagementService : IScopedDependency
{
    Task<GetPosCompanyWithStoresResponse> GetPosCompanyWithStoresAsync(GetPosCompanyWithStoresRequest request, CancellationToken cancellationToken);
    
    Task<GetPosCompanyStoreDetailResponse> GetPosCompanyStoreDetailAsync(GetPosCompanyStoreDetailRequest request, CancellationToken cancellationToken);
    
    Task<CreatePosCompanyStoreResponse> CreatePosCompanyStoreAsync(CreatePosCompanyStoreCommand command,CancellationToken cancellationToken);
    
    Task<UpdatePosCompanyStoreResponse> UpdatePosCompanyStoreAsync(UpdatePosCompanyStoreCommand command,CancellationToken cancellationToken);
    
    Task<DeletePosCompanyStoreResponse> DeletePosCompanyStoreAsync(DeletePosCompanyStoreCommand command,CancellationToken cancellationToken);
    
    Task<UpdatePosCompanyStoreStatusResponse> UpdatePosCompanyStoreStatusAsync(UpdatePosCompanyStoreStatusCommand command,CancellationToken cancellationToken);

    Task<UnbindPosCompanyStoreResponse> UnbindPosCompanyStoreAsync(UnbindPosCompanyStoreCommand command, CancellationToken cancellationToken);
}

public class PosManagementService : IPosManagementService
{
    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IPosManagementDataProvider _posManagementDataProvider;

    public PosManagementService(IMapper mapper, ICurrentUser currentUser, IPosManagementDataProvider posManagementDataProvider)
    {
        _mapper = mapper;
        _currentUser = currentUser;
        _posManagementDataProvider = posManagementDataProvider;
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

        var existingUrlStore = await _posManagementDataProvider.GetPosCompanyStoreAsync(link: command.Link, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (existingUrlStore != null) throw new Exception("PosUrl is currently bound to the store, please enter another posUrl and try again");
        
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
        store.AppleId = null;
        store.PosId = null;
        store.PosName = null;
        
        await _posManagementDataProvider.UpdatePosCompanyStoresAsync([store], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new UnbindPosCompanyStoreResponse
        {
            Data = _mapper.Map<PosCompanyStoreDto>(store)
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