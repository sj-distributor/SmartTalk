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
    Task<GetPosCompanyStoreDetailResponse> GetPosCompanyStoreDetailAsync(GetPosCompanyStoreDetailRequest request, CancellationToken cancellationToken);
    
    Task<CreatePosCompanyStoreResponse> CreatePosCompanyStoreAsync(CreatePosCompanyStoreCommand command,CancellationToken cancellationToken);
    
    Task<UpdatePosCompanyStoreResponse> UpdatePosCompanyStoreAsync(UpdatePosCompanyStoreCommand command,CancellationToken cancellationToken);
    
    Task<DeletePosCompanyStoreResponse> DeletePosCompanyStoreAsync(DeletePosCompanyStoreCommand command,CancellationToken cancellationToken);
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
        var store = await _posManagementDataProvider.GetPosCompanyStoreAsync(command.Id, cancellationToken).ConfigureAwait(false);
            
        _mapper.Map(command, store);

        await _posManagementDataProvider.UpdatePosCompanyStoresAsync([store], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new UpdatePosCompanyStoreResponse
        {
            Data = _mapper.Map<PosCompanyStoreDto>(store)
        };
    }

    public async Task<DeletePosCompanyStoreResponse> DeletePosCompanyStoreAsync(DeletePosCompanyStoreCommand command, CancellationToken cancellationToken)
    {
        var stores = await _posManagementDataProvider.GetPosCompanyStoresAsync([command.StoreId], cancellationToken).ConfigureAwait(false);

        if (stores.Count == 0) throw new Exception("Could not found any stores");

        await _posManagementDataProvider.DeletePosCompanyStoresAsync(stores, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new DeletePosCompanyStoreResponse
        {
            Data = _mapper.Map<List<PosCompanyStoreDto>>(stores)
        };
    }
}