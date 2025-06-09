using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.Pos;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.Embedding;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Dto.Smarties;
using SmartTalk.Messages.Dto.VectorDb;

namespace SmartTalk.Core.Services.Pos;

public partial interface IPosService : IScopedDependency
{
    Task<SyncPosConfigurationResponse> SyncPosConfigurationAsync(SyncPosConfigurationCommand command, CancellationToken cancellationToken);
}

public partial class PosService 
{
    public async Task<SyncPosConfigurationResponse> SyncPosConfigurationAsync(SyncPosConfigurationCommand command, CancellationToken cancellationToken)
    {
        var store = await _posDataProvider.GetPosCompanyStoreAsync(id: command.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (store == null) throw new Exception($"Can't find store with id：，StoreId: {command.StoreId}");
        
        Log.Information("Get the store info: {@store}", store);
        
        var posConfiguration = await _easyPosClient.GetPosCompanyStoreMenusAsync(
            new EasyPosTokenRequestDto { AppId = store.AppId, AppSecret = store.AppSecret }, cancellationToken).ConfigureAwait(false);

        Log.Information("Get the pos configuration: {@posConfiguration}", posConfiguration);
        
        var storeOpeningHours = posConfiguration?.Data?.TimePeriods; 
        await UpdateStoreBusinessTimePeriodsAsync(store, storeOpeningHours, cancellationToken).ConfigureAwait(false);
        
        var products = await SyncMenuDataAsync(store, posConfiguration?.Data, cancellationToken).ConfigureAwait(false);
        
        await PosProductsVectorizationAsync(products, store, cancellationToken).ConfigureAwait(false);
        
        return new SyncPosConfigurationResponse
        {
            Data = _mapper.Map<List<PosProductDto>>(products)
        };
    }
    
    private async Task UpdateStoreBusinessTimePeriodsAsync(PosCompanyStore store, List<EasyPosResponseTimePeriod> timePeriods, CancellationToken cancellationToken)
    {
        store.TimePeriod = timePeriods != null && timePeriods.Count != 0 ? JsonConvert.SerializeObject(timePeriods.First()) : string.Empty;
        await _posDataProvider.UpdateStoreAsync(store, true, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<PosProduct>> SyncMenuDataAsync(PosCompanyStore store, EasyPosResponseData data, CancellationToken cancellationToken)
    {
        if (data?.Menus == null) throw new NullReferenceException("Pos Resource Data or Menus is null");
        
        await _posDataProvider.DeletePosMenuInfosAsync(store.Id, cancellationToken: cancellationToken).ConfigureAwait(false);

        var menuMap = await AddPosMenusAsync(data.Menus, store.Id, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Sync menu data: {@MenuMap}", menuMap);
        
        var categoriesMap = await AddPosCategoriesAsync(data, menuMap, store.Id, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Sync categories data: {@CategoriesMap}", categoriesMap);
        
        return await AddPosProductsAsync(data, categoriesMap, store.Id, cancellationToken).ConfigureAwait(false);
    }
    
    private async Task<Dictionary<string, int>> AddPosMenusAsync(List<EasyPosResponseMenu> menus, int storeId, CancellationToken cancellationToken)
    {
        var posMenus = menus.Select(x => new PosMenu
        {
            StoreId = storeId,
            MenuId = x.Id.ToString(),
            Names = JsonConvert.SerializeObject(GetLocalizedNames(x.Localizations)),
            TimePeriod = JsonConvert.SerializeObject(x.TimePeriods),
            CategoryIds = x.CategoryIds == null ? string.Empty : string.Join(",", x.CategoryIds),
            Status = x.Status,
            CreatedBy = _currentUser.Id
        }).ToList();
        
        await _posDataProvider.AddPosMenusAsync(posMenus, true, cancellationToken).ConfigureAwait(false);
        
        return posMenus.ToDictionary(m => m.MenuId, m => m.Id);
    }
    
    private async Task<Dictionary<long, Dictionary<string, int>>> AddPosCategoriesAsync(EasyPosResponseData data, Dictionary<string, int> menuMap, int storeId, CancellationToken cancellationToken)
    {
        var posCategories = new List<PosCategory>();
        var mapping = new Dictionary<long, List<PosCategory>>();
        
        foreach (var menu in data.Menus)
        {
            if (!menuMap.TryGetValue(menu.Id.ToString(), out var posMenuId))
                continue;

            var categories = menu.Categories.Where(c => c.MenuIds.Contains(menu.Id)).Select(x => new PosCategory
            {
                MenuId = posMenuId,
                StoreId = storeId,
                CategoryId = x.Id.ToString(),
                Names = JsonConvert.SerializeObject(GetLocalizedNames(x.Localizations)),
                MenuIds = string.Join(",", x.MenuIds ?? []),
                MenuNames = JsonConvert.SerializeObject(GetLocalizedNames(menu.Localizations)),
                CreatedBy = _currentUser.Id
            }).ToList();
            
            posCategories.AddRange(categories);
            mapping[menu.Id] = categories;
        }
        
        await _posDataProvider.AddPosCategoriesAsync(posCategories, true, cancellationToken).ConfigureAwait(false);

        return BuildMenuToCategoriesMapping(mapping);
    }

    private Dictionary<long, Dictionary<string, int>> BuildMenuToCategoriesMapping(Dictionary<long, List<PosCategory>> menuToCategoriesMap)
    {
        var mapping = new Dictionary<long, Dictionary<string, int>>();

        foreach (var (menuId, originalCategories) in menuToCategoriesMap)
        {
            mapping[menuId] = originalCategories.ToDictionary(c => c.CategoryId, c => c.Id);
        }
        
        return mapping;
    }
    
    private async Task<List<PosProduct>> AddPosProductsAsync(EasyPosResponseData data, Dictionary<long, Dictionary<string, int>> categoriesMap, int storeId, CancellationToken cancellationToken)
    {
        var posProducts = new List<PosProduct>();
        
        foreach (var menu in data.Menus)
        {
            foreach (var category in menu.Categories.Where(c => c.MenuIds.Contains(menu.Id)))
            {
                if (categoriesMap.TryGetValue(menu.Id, out var categoryMap) && categoryMap.TryGetValue(category.Id.ToString(), out var posCategoryId))
                {
                    var products = category.Products.Where(p => p.CategoryIds.Contains(category.Id) && p.IsIndependentSale)
                        .Select(product => new PosProduct
                        {
                            StoreId = storeId,
                            ProductId = product.Id.ToString(),
                            CategoryId = posCategoryId,
                            Price = product.Price,
                            Status = true,
                            Names = JsonConvert.SerializeObject(GetLocalizedNames(product.Localizations)),
                            Modifiers = product.ModifierGroups != null ? JsonConvert.SerializeObject(product.ModifierGroups) : null,
                            Tax = product.Taxes != null ? JsonConvert.SerializeObject(product.Taxes) : null,
                            CategoryIds = string.Join(",", product.CategoryIds ?? []),
                            CreatedBy = _currentUser.Id
                        }).ToList();
                
                    posProducts.AddRange(products);
                }
            }
        }
        
        await _posDataProvider.AddPosProductsAsync(posProducts, true, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Sync products data completed");
        
        return posProducts;
    }
    
    private Dictionary<string, Dictionary<string, string>> GetLocalizedNames(IEnumerable<EasyPosResponseLocalization> localizations)
    {
        var languageCodeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["zh_CN"] = "cn",
            ["en_US"] = "en"
        };

        return localizations
            .Where(loc => !string.IsNullOrWhiteSpace(loc.LanguageCode) && !string.IsNullOrWhiteSpace(loc.Field))
            .GroupBy(loc => loc.LanguageCode)
            .Where(g => languageCodeMap.ContainsKey(g.Key))
            .ToDictionary(
                g => languageCodeMap[g.Key],
                g => g.ToDictionary(x => x.Field, x => x.Value)
            );
    }

    public async Task PosProductsVectorizationAsync(List<PosProduct> products, PosCompanyStore store, CancellationToken cancellationToken)
    {
        await CheckIndexIfExistsAsync(store, cancellationToken).ConfigureAwait(false);
        
        foreach (var product in products)
            await DeleteInternalAsync(store, product, cancellationToken).ConfigureAwait(false);
        
        foreach (var product in products)
            _smartTalkBackgroundJobClient.Enqueue(() => StoreAsync(store, product, cancellationToken), HangfireConstants.InternalHostingRestaurant);
    }

    public async Task CheckIndexIfExistsAsync(PosCompanyStore store, CancellationToken cancellationToken)
    {
        var indexes = await _vectorDb.GetIndexesAsync(cancellationToken).ConfigureAwait(false);
        
        if (!indexes.Any(x => x == $"pos-{store.Id}"))
            await _vectorDb.CreateIndexAsync($"pos-{store.Id}", 3072, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteInternalAsync(PosCompanyStore store, PosProduct product, CancellationToken cancellationToken)
    {
        var productNames = ParseProductNames(product.Names);

        var languageCodes = productNames.Where(name => !string.IsNullOrWhiteSpace(name.Value)).Select(x => x.Key).ToList();
            
        foreach (var languageCode in languageCodes)
            await _vectorDb.DeleteAsync($"pos-{store.Id}", new VectorRecordDto { Id = $"{languageCode}{product.Id}" }, cancellationToken).ConfigureAwait(false);
    }
    
    public async Task StoreAsync(PosCompanyStore store, PosProduct product, CancellationToken cancellationToken)
    {
        var productNames = ParseProductNames(product.Names);
        
        foreach (var productName in productNames.Where(name => !string.IsNullOrWhiteSpace(name.Value)))
            _smartTalkBackgroundJobClient.Enqueue(() => StoreInternalAsync(store, product, productName.Key, productName.Value, cancellationToken), HangfireConstants.InternalHostingRestaurant);
    }

    public async Task StoreInternalAsync(PosCompanyStore store, PosProduct product, string languageCode, string productName, CancellationToken cancellationToken)
    {
        var record = new VectorRecordDto { Id = $"{languageCode}{product.Id}" };
        
        var response = await _smartiesClient.GetEmbeddingAsync(new AskGptEmbeddingRequestDto { Input = productName }, cancellationToken).ConfigureAwait(false);
        
        if (response == null || !response.Data.Data.Any()) throw new Exception($"Failed to embed content with id {languageCode}-{product.Id}");
        
        record.Vector = new EmbeddingDto(response.Data.Data.First().Embedding.ToArray());

        var payload = _mapper.Map<PosProductPayloadDto>(product);
        payload.LanguageCode = languageCode;

        record.Payload[VectorDbStore.ReservedPosProductPayload] = payload;
        
        await _vectorDb.UpsertAsync($"pos-{store.Id}", record, cancellationToken).ConfigureAwait(false);
    }
    
    public Dictionary<string, string> ParseProductNames(string namesJson)
    {
        if (string.IsNullOrWhiteSpace(namesJson)) return new Dictionary<string, string>();

        var names = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(namesJson);

        var enName = names?.GetValueOrDefault("en")?.GetValueOrDefault("posName");
        var cnName = names?.GetValueOrDefault("cn")?.GetValueOrDefault("posName");

        if (string.IsNullOrWhiteSpace(enName))
            enName = names?.GetValueOrDefault("en")?.GetValueOrDefault("name");

        if (string.IsNullOrWhiteSpace(cnName))
            cnName = names?.GetValueOrDefault("cn")?.GetValueOrDefault("name");

        return new Dictionary<string, string> { { "cn", cnName }, { "en", enName } };
    }
}