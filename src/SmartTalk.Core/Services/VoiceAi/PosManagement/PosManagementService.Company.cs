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

    Task<GetPosMenuPreviewResponse> GetPosMenuPreviewAsync(GetPosMenuPreviewRequest request, CancellationToken cancellationToken);

    Task<GetPosMenuDetailResponse> GetPosMenuDetailAsync(GetPosMenuDetailRequest request, CancellationToken cancellationToken);

    Task<GetPosCategoryResponse> GetPosCategoryAsync(GetPosCategoryRequest request, CancellationToken cancellationToken);
    
    Task<GetPosProductResponse> GetPosProductAsync(GetPosProductRequest request, CancellationToken cancellationToken);

    Task<UpdatePosCategoryResponse> UpdatePosCategoryAsync(UpdatePosCategoryCommand command, CancellationToken cancellationToken);
    
    Task<UpdatePosProductResponse> UpdatePosProductAsync(UpdatePosProductCommand command, CancellationToken cancellationToken);
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
        var menu = await _posManagementDataProvider.GetPosMenuAsync(id: command.Id, cancellationToken: cancellationToken).ConfigureAwait(false);

        menu.Status = command.Status;
        if (!string.IsNullOrEmpty(command.TimePeriod))
            menu.TimePeriod = command.TimePeriod;

        await _posManagementDataProvider.UpdatePosMenuAsync(menu, true, cancellationToken).ConfigureAwait(false);

        return new UpdatePosMenuResponse()
        {
            Data = _mapper.Map<PosMenuDto>(menu)
        };
    }

    public async Task<GetPosMenuPreviewResponse> GetPosMenuPreviewAsync(GetPosMenuPreviewRequest request, CancellationToken cancellationToken)
    {
        var menus = await _posManagementDataProvider.GetPosMenusAsync(request.StoreId, cancellationToken).ConfigureAwait(false);
        
        var categories = await _posManagementDataProvider.GetPosCategoriesAsync(storeId: request.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var products = await _posManagementDataProvider.GetPosProductsAsync(storeId: request.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);

        var menuWithCategoriesList = menus.Select(menu =>
        {
            var menuCategories = categories.Where(c => c.MenuId == menu.Id).ToList();

            var categoryWithProducts = menuCategories.Select(category =>
            {
                var categoryProducts = products.Where(p => p.CategoryId == category.Id).ToList();

                return new PosCategoryWithProduct
                {
                    Category = _mapper.Map<PosCategoryDto>(category),
                    Products = _mapper.Map<List<PosProductDto>>(categoryProducts)
                };
            }).ToList();

            return new PosMenuWithCategories
            {
                Menu = _mapper.Map<PosMenuDto>(menu),
                PosCategoryWithProduct = categoryWithProducts
            };
        }).ToList();

        return new GetPosMenuPreviewResponse
        {
            Data = new PosMenuPreviewData
            {
                MenuWithCategories = menuWithCategoriesList
            }
        };
    }

    public async Task<GetPosMenuDetailResponse> GetPosMenuDetailAsync(GetPosMenuDetailRequest request, CancellationToken cancellationToken)
    {
        var menu = await _posManagementDataProvider.GetPosMenuAsync(id: request.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (menu == null) throw new Exception("Can't find menu with id:" + request.Id);

        return new GetPosMenuDetailResponse()
        {
            Data = _mapper.Map<PosMenuDto>(menu)
        };
    }

    public async Task<GetPosCategoryResponse> GetPosCategoryAsync(GetPosCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await _posManagementDataProvider.GetPosCategoriesAsync(id: request.Id,  cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (category == null) throw new Exception("Can't find category with id:" + request.Id);

        return new GetPosCategoryResponse()
        {
            Data = _mapper.Map<PosCategoryDto>(category)
        };
    }

    public async Task<GetPosProductResponse> GetPosProductAsync(GetPosProductRequest request, CancellationToken cancellationToken)
    {
        var product = await _posManagementDataProvider.GetPosProductsAsync(id: request.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (product == null) throw new Exception("Can't find product with id:" + request.Id);

        return new GetPosProductResponse()
        {
            Data = _mapper.Map<PosProductDto>(product)
        };
    }

    public async Task<UpdatePosCategoryResponse> UpdatePosCategoryAsync(UpdatePosCategoryCommand command, CancellationToken cancellationToken)
    {
        var categories = await _posManagementDataProvider.GetPosCategoriesAsync(id: command.Id,  cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (categories == null || !categories.Any()) throw new InvalidOperationException("No category found with the specified ID.");

        categories.First().Names = command.Names;

        await _posManagementDataProvider.UpdateCategoriesAsync(categories, true, cancellationToken).ConfigureAwait(false);

        return new UpdatePosCategoryResponse()
        {
            Data = _mapper.Map<List<PosCategoryDto>>(categories)
        };
    }

    public async Task<UpdatePosProductResponse> UpdatePosProductAsync(UpdatePosProductCommand command, CancellationToken cancellationToken)
    {
        var products = await _posManagementDataProvider.GetPosProductsAsync(id: command.Id, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (products == null || !products.Any()) throw new InvalidOperationException("No product found with the specified ID.");
        
        products.First().Names = command.Names;

        await _posManagementDataProvider.UpdateProductsAsync(products, true, cancellationToken).ConfigureAwait(false);

        return new UpdatePosProductResponse()
        {
            Data = _mapper.Map<List<PosProductDto>>(products)
        };
    }
}