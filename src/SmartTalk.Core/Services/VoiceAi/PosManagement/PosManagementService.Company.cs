using Aliyun.OSS;
using SmartTalk.Core.Domain.VoiceAi.PosManagement;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.VoiceAi.PosManagement;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Events.VoiceAi.PosManagement;
using SmartTalk.Messages.Requests.VoiceAi.PosManagement;

namespace SmartTalk.Core.Services.VoiceAi.PosManagement;

public partial interface IPosManagementService : IScopedDependency
{
    Task<PosCompanyCreatedEvent> CreatePosCompanyAsync(CreatePosCompanyCommand command, CancellationToken cancellationToken);

    Task<PosCompanyUpdatedEvent> UpdatePosCompanyAsync(UpdatePosCompanyCommand command, CancellationToken cancellationToken);

    Task<PosCompanyUpdatedStatusEvent> UpdatePosCompanyStatusAsync(UpdatePosCompanyStatusCommand command, CancellationToken cancellationToken);
    
    Task<PosCompanyDeletedEvent> DeletePosCompanyAsync(DeletePosCompanyCommand command, CancellationToken cancellationToken);

    Task<GetPosCompanyDetailResponse> GetPosCompanyDetailAsync(GetPosCompanyDetailRequest request, CancellationToken cancellationToken);

    Task<GetPosMenusListResponse> GetPosMenusListAsync(GetPosMenusListRequest request, CancellationToken cancellationToken);

    Task<UpdatePosMenuResponse> UpdatePosMenuAsync(UpdatePosMenuCommand command, CancellationToken cancellationToken);

    Task<GetPosMenuPreviewResponse> GetPosMenuPreviewAsync(GetPosMenuPrviewRequest request, CancellationToken cancellationToken);
}

public partial class PosManagementService : IPosManagementService
{
    public async Task<PosCompanyCreatedEvent> CreatePosCompanyAsync(CreatePosCompanyCommand command, CancellationToken cancellationToken)
    {
        var company = new PosCompany
        {
            Name = command.Name, Description = command.Description, Status = false
        };

        await _posManagementDataProvider.CreatePosCompanyAsync(company, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new PosCompanyCreatedEvent
        {
            Company = _mapper.Map<PosCompanyDto>(company)
        };
    }

    public async Task<PosCompanyUpdatedEvent> UpdatePosCompanyAsync(UpdatePosCompanyCommand command, CancellationToken cancellationToken)
    {
        var company = await _posManagementDataProvider.GetPosCompanyAsync(command.Id, cancellationToken).ConfigureAwait(false);

        company.Name = command.Name;
        company.Description = command.Description;

        await _posManagementDataProvider.UpdatePosCompanyAsync(company, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new PosCompanyUpdatedEvent
        {
            Company = _mapper.Map<PosCompanyDto>(company)
        };
    }

    public async Task<PosCompanyUpdatedStatusEvent> UpdatePosCompanyStatusAsync(UpdatePosCompanyStatusCommand command, CancellationToken cancellationToken)
    {
        var company = await _posManagementDataProvider.GetPosCompanyAsync(command.Id, cancellationToken).ConfigureAwait(false);

        company.Status = command.Status;

        await _posManagementDataProvider.UpdatePosCompanyAsync(company, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new PosCompanyUpdatedStatusEvent
        {
            Company = _mapper.Map<PosCompanyDto>(company)
        };
    }

    public async Task<PosCompanyDeletedEvent> DeletePosCompanyAsync(DeletePosCompanyCommand command, CancellationToken cancellationToken)
    {
        var company = await _posManagementDataProvider.GetPosCompanyAsync(command.Id, cancellationToken).ConfigureAwait(false);

        if (company == null) throw new Exception("Can't find company with id:" + command.Id);
        
        await _posManagementDataProvider.DeletePosCompanyAsync(company, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var stores = await _posManagementDataProvider.GetPosCompanyStoresAsync(companyIds: [company.Id], cancellationToken: cancellationToken).ConfigureAwait(false);
        await _posManagementDataProvider.DeletePosCompanyStoresAsync(stores: stores, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new PosCompanyDeletedEvent
        {
            Company = _mapper.Map<PosCompanyDto>(company)
        };
    }

    public async Task<GetPosCompanyDetailResponse> GetPosCompanyDetailAsync(GetPosCompanyDetailRequest request, CancellationToken cancellationToken)
    {
        var company = await _posManagementDataProvider.GetPosCompanyAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (company == null) throw new Exception("Can't find company with id:" + request.Id);
        
        return new GetPosCompanyDetailResponse
        {
            Data = _mapper.Map<PosCompanyDto>(company)
        };
    }

    public async Task<GetPosMenusListResponse> GetPosMenusListAsync(GetPosMenusListRequest request, CancellationToken cancellationToken)
    {
        var menus = await _posManagementDataProvider.GetPosMenusAsync(request.StoreId, cancellationToken).ConfigureAwait(false);
        
        if (menus == null) throw new Exception("Can't find menus with id:" + request.StoreId);

        return new GetPosMenusListResponse()
        {
            Data = _mapper.Map<List<PosMenuDto>>(menus)
        };
    }

    public async Task<UpdatePosMenuResponse> UpdatePosMenuAsync(UpdatePosMenuCommand command, CancellationToken cancellationToken)
    {
        var menu = await _posManagementDataProvider.GetPosMenuAsync(command.MenuId, null, cancellationToken).ConfigureAwait(false);

        menu.Status = command.Status;
        menu.TimePeriod = command.TimePeriod;

        await _posManagementDataProvider.UpdatePosMenuAsync(menu, true, cancellationToken).ConfigureAwait(false);

        return new UpdatePosMenuResponse()
        {
            Data = _mapper.Map<PosMenuDto>(menu)
        };
    }

    public async Task<GetPosMenuPreviewResponse> GetPosMenuPreviewAsync(GetPosMenuPrviewRequest request, CancellationToken cancellationToken)
    {
        var menu = await _posManagementDataProvider.GetPosMenuAsync(null, request.MenuId, cancellationToken).ConfigureAwait(false);

        var categories = await _posManagementDataProvider.GetPosCategoriesAsync(menu.Id, cancellationToken).ConfigureAwait(false);

        var categoryTasks = categories.Select(async category =>
        {
            var products = await _posManagementDataProvider.GetPosProductsAsync(category.Id, cancellationToken).ConfigureAwait(false);

            return new PosCategoryWithProduct
            {
                Category = _mapper.Map<PosCategoryDto>(category),
                Products = _mapper.Map<List<PosProductDto>>(products)
            };
        });

        var categoryWithProducts = await Task.WhenAll(categoryTasks);

        return new GetPosMenuPreviewResponse
        {
            Data = new PosMenuPreviewData
            {
                CategoryWithProduct = categoryWithProducts.ToList()
            }
        };
    }
}