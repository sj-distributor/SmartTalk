using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.Pos;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Events.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Services.Pos;

public partial interface IPosService : IScopedDependency
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

public partial class PosService : IPosService
{
    public async Task<PosCompanyCreatedEvent> CreatePosCompanyAsync(CreatePosCompanyCommand command, CancellationToken cancellationToken)
    {
        var company = new PosCompany
        {
            Name = command.Name, Description = command.Description, Status = false
        };

        await _posDataProvider.CreatePosCompanyAsync(company, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new PosCompanyCreatedEvent
        {
            Company = _mapper.Map<PosCompanyDto>(company)
        };
    }

    public async Task<PosCompanyUpdatedEvent> UpdatePosCompanyAsync(UpdatePosCompanyCommand command, CancellationToken cancellationToken)
    {
        var company = await _posDataProvider.GetPosCompanyAsync(command.Id, cancellationToken).ConfigureAwait(false);

        company.Name = command.Name;
        company.Description = command.Description;

        await _posDataProvider.UpdatePosCompanyAsync(company, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new PosCompanyUpdatedEvent
        {
            Company = _mapper.Map<PosCompanyDto>(company)
        };
    }

    public async Task<PosCompanyUpdatedStatusEvent> UpdatePosCompanyStatusAsync(UpdatePosCompanyStatusCommand command, CancellationToken cancellationToken)
    {
        var company = await _posDataProvider.GetPosCompanyAsync(command.Id, cancellationToken).ConfigureAwait(false);

        company.Status = command.Status;

        await _posDataProvider.UpdatePosCompanyAsync(company, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new PosCompanyUpdatedStatusEvent
        {
            Company = _mapper.Map<PosCompanyDto>(company)
        };
    }

    public async Task<PosCompanyDeletedEvent> DeletePosCompanyAsync(DeletePosCompanyCommand command, CancellationToken cancellationToken)
    {
        var company = await _posDataProvider.GetPosCompanyAsync(command.Id, cancellationToken).ConfigureAwait(false);

        if (company == null) throw new Exception("Can't find company with id:" + command.Id);
        
        await _posDataProvider.DeletePosCompanyAsync(company, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var stores = await _posDataProvider.GetPosCompanyStoresAsync(companyIds: [company.Id], cancellationToken: cancellationToken).ConfigureAwait(false);
        await _posDataProvider.DeletePosCompanyStoresAsync(stores: stores, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new PosCompanyDeletedEvent
        {
            Company = _mapper.Map<PosCompanyDto>(company)
        };
    }

    public async Task<GetPosCompanyDetailResponse> GetPosCompanyDetailAsync(GetPosCompanyDetailRequest request, CancellationToken cancellationToken)
    {
        var company = await _posDataProvider.GetPosCompanyAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (company == null) throw new Exception("Can't find company with id:" + request.Id);
        
        return new GetPosCompanyDetailResponse
        {
            Data = _mapper.Map<PosCompanyDto>(company)
        };
    }

    public async Task<GetPosMenusListResponse> GetPosMenusListAsync(GetPosMenusListRequest request, CancellationToken cancellationToken)
    {
        var menus = await _posDataProvider.GetPosMenusAsync(request.StoreId, cancellationToken).ConfigureAwait(false);
        
        if (menus == null) throw new Exception("Can't find menus with id:" + request.StoreId);

        return new GetPosMenusListResponse()
        {
            Data = _mapper.Map<List<PosMenuDto>>(menus)
        };
    }

    public async Task<UpdatePosMenuResponse> UpdatePosMenuAsync(UpdatePosMenuCommand command, CancellationToken cancellationToken)
    {
        var menu = await _posDataProvider.GetPosMenuAsync(id: command.Id, cancellationToken: cancellationToken).ConfigureAwait(false);

        menu.Status = command.Status;
        if (!string.IsNullOrEmpty(command.TimePeriod))
            menu.TimePeriod = command.TimePeriod;

        await _posDataProvider.UpdatePosMenuAsync(menu, true, cancellationToken).ConfigureAwait(false);

        return new UpdatePosMenuResponse()
        {
            Data = _mapper.Map<PosMenuDto>(menu)
        };
    }

    public async Task<GetPosMenuPreviewResponse> GetPosMenuPreviewAsync(GetPosMenuPreviewRequest request, CancellationToken cancellationToken)
    {
        var menus = await _posDataProvider.GetPosMenusAsync(request.StoreId, cancellationToken).ConfigureAwait(false);
        
        var categories = await _posDataProvider.GetPosCategoriesAsync(storeId: request.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var products = await _posDataProvider.GetPosProductsAsync(storeId: request.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);

        var menuWithCategoriesList = Enumerable.Select<PosMenu, PosMenuWithCategories>(menus, menu =>
        {
            var menuCategories = Enumerable.Where<PosCategory>(categories, c => c.MenuId == menu.Id).ToList();

            var categoryWithProducts = menuCategories.Select(category =>
            {
                var categoryProducts = Enumerable.Where<PosProduct>(products, p => p.CategoryId == category.Id).ToList();

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
        var menu = await _posDataProvider.GetPosMenuAsync(id: request.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (menu == null) throw new Exception("Can't find menu with id:" + request.Id);

        return new GetPosMenuDetailResponse()
        {
            Data = _mapper.Map<PosMenuDto>(menu)
        };
    }

    public async Task<GetPosCategoryResponse> GetPosCategoryAsync(GetPosCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await _posDataProvider.GetPosCategoriesAsync(id: request.Id,  cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (category == null) throw new Exception("Can't find category with id:" + request.Id);

        return new GetPosCategoryResponse()
        {
            Data = _mapper.Map<List<PosCategoryDto>>(category)
        };
    }

    public async Task<GetPosProductResponse> GetPosProductAsync(GetPosProductRequest request, CancellationToken cancellationToken)
    {
        var product = await _posDataProvider.GetPosProductsAsync(id: request.Id, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (product == null) throw new Exception("Can't find product with id:" + request.Id);

        return new GetPosProductResponse()
        {
            Data = _mapper.Map<PosProductDto>(product)
        };
    }

    public async Task<UpdatePosCategoryResponse> UpdatePosCategoryAsync(UpdatePosCategoryCommand command, CancellationToken cancellationToken)
    {
        var categories = await _posDataProvider.GetPosCategoriesAsync(id: command.Id,  cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (categories == null || !Enumerable.Any<PosCategory>(categories)) throw new InvalidOperationException("No category found with the specified ID.");

        Enumerable.First<PosCategory>(categories).Names = command.Names;

        await _posDataProvider.UpdateCategoriesAsync(categories, true, cancellationToken).ConfigureAwait(false);

        return new UpdatePosCategoryResponse()
        {
            Data = _mapper.Map<List<PosCategoryDto>>(categories)
        };
    }

    public async Task<UpdatePosProductResponse> UpdatePosProductAsync(UpdatePosProductCommand command, CancellationToken cancellationToken)
    {
        var products = await _posDataProvider.GetPosProductsAsync(id: command.Id, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (products == null || !Enumerable.Any<PosProduct>(products)) throw new InvalidOperationException("No product found with the specified ID.");
        
        Enumerable.First<PosProduct>(products).Names = command.Names;

        await _posDataProvider.UpdateProductsAsync(products, true, cancellationToken).ConfigureAwait(false);

        return new UpdatePosProductResponse()
        {
            Data = _mapper.Map<List<PosProductDto>>(products)
        };
    }
}