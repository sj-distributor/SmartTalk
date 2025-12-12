using Autofac.Core;
using AutoMapper;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.Pos;

namespace SmartTalk.Core.Services.Pos;

public partial interface IPosDataProvider : IScopedDependency
{
    Task<(int Count, List<Company> Companies)> GetPosCompaniesAsync(
        int? pageIndex = null, int? pageSize = null, List<int> companyIds = null, int? serviceProviderId = null, string keyword = null, CancellationToken cancellationToken = default);
        
    Task<CompanyStore> GetPosCompanyStoreAsync(
        string link = null, int? id = null, string appId = null, string appSecret = null, CancellationToken cancellationToken = default);
    
    Task<CompanyStoreDto> GetPosCompanyStoreDetailAsync(int? id = null, CancellationToken cancellationToken = default);
    
    Task<List<CompanyStore>> GetPosCompanyStoresAsync(List<int> ids = null, List<int> companyIds = null, string keyword = null, CancellationToken cancellationToken = default);
    
    Task AddPosCompanyStoresAsync(List<CompanyStore> stores, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdatePosCompanyStoresAsync(List<CompanyStore> stores, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task DeletePosCompanyStoresAsync(List<CompanyStore> stores, bool forceSave = true, CancellationToken cancellationToken = default);

    Task CreatePosStoreUserAsync(List<StoreUser> posStoreUsers, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task DeletePosStoreUsersAsync(List<StoreUser> posStoreUsers, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<List<StoreUserDto>> GetPosStoreUsersAsync(int storeId, CancellationToken cancellationToken = default);
    
    Task<List<CompanyStoreDto>> GetPosCompanyStoresWithSortingAsync(List<int> storeIds = null,
        int? companyId = null, int? serviceProviderId = null, string keyword = null, bool isNormalSort = false, CancellationToken cancellationToken = default);

    Task<List<StoreUser>> GetPosStoreUsersByUserIdAsync(int userId, CancellationToken cancellationToken);
    
    Task AddPosAgentsAsync(List<PosAgent> agents, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<CompanyStore> GetPosStoreByAgentIdAsync(int agentId, CancellationToken cancellationToken = default);

    Task<List<CompanyStore>> GetPosStoresByAgentIdsAsync(List<int> agentIds, CancellationToken cancellationToken = default);
    
    Task<List<PosAgent>> GetPosAgentsAsync(List<int> storeIds = null, int? agentId = null, CancellationToken cancellationToken = default);

    Task<StoreUser> GetPosStoreUsersByUserIdAndAssistantIdAsync(List<int> assistantIds, int userId, CancellationToken cancellationToken = default);

    Task<List<PosAgent>> GetPosAgentByUserIdAsync(int userId, CancellationToken cancellationToken);

    Task DeletePosAgentsByAgentIdsAsync(List<int> agentIds, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<List<ServiceProvider>> GetServiceProviderByIdAsync(int? serviceProviderId = null, CancellationToken cancellationToken = default);
    
    Task<List<(CompanyStore Store, Agent Agent)>> GetStoresAndAgentsAsync(int? serviceProviderId = null, CancellationToken cancellationToken = default);
    
    Task<List<SimpleStoreAgentDto>> GetSimpleStoreAgentsAsync(int? serviceProviderId = null, CancellationToken cancellationToken = default);
    
    Task<List<CompanyStore>> GetAllStoresAsync(int? serviceProviderId = null, CancellationToken cancellationToken = default);
    
    Task<PosAgent> GetPosAgentByAgentIdAsync(int agentId, CancellationToken cancellationToken);

    Task<List<(PosCategory, PosProduct)>> GetPosCategoryAndProductsAsync(int storeId, CancellationToken cancellationToken);
}

public partial class PosDataProvider : IPosDataProvider
{
    private readonly IMapper _mapper;
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public PosDataProvider(IMapper mapper, IRepository repository, IUnitOfWork unitOfWork)
    {
        _mapper = mapper;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<(int Count, List<Company> Companies)> GetPosCompaniesAsync(
        int? pageIndex = null, int? pageSize = null, List<int> companyIds = null, int? serviceProviderId = null, string keyword = null, CancellationToken cancellationToken = default)
    {
        var query = from company in _repository.Query<Company>()
            join store in _repository.Query<CompanyStore>() on company.Id equals store.CompanyId into storeGroups
            from store in storeGroups.DefaultIfEmpty()
            where (!serviceProviderId.HasValue || company.ServiceProviderId == serviceProviderId.Value)
                  && (companyIds == null || companyIds.Count == 0 || companyIds.Contains(company.Id))
                  && (string.IsNullOrEmpty(keyword) || company.Name.Contains(keyword) || (store != null && store.Names.Contains(keyword)))
            select company;

        query = query.Distinct();
        
        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        if (pageIndex.HasValue && pageSize.HasValue)
            query = query.OrderByDescending(x => x.CreatedDate).Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
        
        var companies = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        return (count, companies);
    }

    public async Task<CompanyStore> GetPosCompanyStoreAsync(
        string link = null, int? id = null, string appId = null, string appSecret = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<CompanyStore>();

        if (id.HasValue)
            query = query.Where(x => x.Id == id.Value);

        if (!string.IsNullOrEmpty(link))
            query = query.Where(x => x.Link == link);
        
        if (!string.IsNullOrEmpty(appId))
            query = query.Where(x => x.AppId == appId);
        
        if (!string.IsNullOrEmpty(appSecret))
            query = query.Where(x => x.AppSecret == appSecret);

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CompanyStoreDto> GetPosCompanyStoreDetailAsync(int? id = null, CancellationToken cancellationToken = default)
    {
        var query = from company in _repository.Query<Company>()
            join store in _repository.Query<CompanyStore>() on company.Id equals store.CompanyId
            select new CompanyStoreDto
            {
                Id = store.Id,
                CompanyId = company.Id,
                Names = store.Names,
                Description = store.Description,
                CompanyDescription = company.Description,
                Status = store.Status,
                PhoneNums = store.PhoneNums,
                Logo = store.Logo,
                Address = store.Address,
                Latitude = store.Latitude,
                Longitude = store.Longitude,
                Link = store.Link,
                AppId = store.AppId,
                IsLink = store.IsLink,
                PosId = store.PosId,
                PosName = store.PosName,
                TimePeriod = store.TimePeriod,
                Timezone = store.Timezone,
                IsManualReview = store.IsManualReview,
                CreatedBy = store.CreatedBy,
                CreatedDate = store.CreatedDate,
                LastModifiedBy = store.LastModifiedBy,
                LastModifiedDate = store.LastModifiedDate
            };

        if (id.HasValue)
            query = query.Where(x => x.Id == id.Value);

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<CompanyStore>> GetPosCompanyStoresAsync(List<int> ids = null, List<int> companyIds = null, string keyword = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<CompanyStore>();

        if (ids != null && ids.Count != 0)
            query = query.Where(x => ids.Contains(x.Id));
        
        if (companyIds != null && companyIds.Count != 0)
            query = query.Where(x => companyIds.Contains(x.CompanyId));
        
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(x => x.Names.Contains(keyword));

        return await query.OrderByDescending(x => x.CreatedDate).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddPosCompanyStoresAsync(List<CompanyStore> stores, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(stores, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdatePosCompanyStoresAsync(List<CompanyStore> stores, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(stores, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeletePosCompanyStoresAsync(List<CompanyStore> stores, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAllAsync(stores, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CreatePosStoreUserAsync(List<StoreUser> posStoreUsers, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(posStoreUsers, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeletePosStoreUsersAsync(List<StoreUser> posStoreUsers, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAllAsync(posStoreUsers, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<StoreUserDto>> GetPosStoreUsersAsync(int storeId, CancellationToken cancellationToken = default)
    {
        var query = from storeUser in _repository.Query<StoreUser>()
            join userAccount in _repository.Query<UserAccount>() on storeUser.UserId equals userAccount.Id into userAccounts
            from userAccount in userAccounts.DefaultIfEmpty()
            where storeUser.StoreId == storeId
            select new StoreUserDto()
            {
                Id = storeUser.Id,
                UserId = storeUser.UserId,
                StoreId = storeUser.StoreId,
                CreatedBy = storeUser.CreatedBy,
                CreatedDate = storeUser.CreatedDate,
                LastModifiedBy = storeUser.LastModifiedBy,
                LastModifiedDate = storeUser.LastModifiedDate,
                UserName = userAccount != null ? userAccount.UserName : $"用户{storeUser.UserId}"
            };

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<CompanyStoreDto>> GetPosCompanyStoresWithSortingAsync(
        List<int> storeIds = null, int? companyId = null, int? serviceProviderId = null, string keyword = null, bool isNormalSort = false, CancellationToken cancellationToken = default)
    {
        var query = from store in _repository.Query<CompanyStore>().Where(x => x.Status)
            join company in _repository.Query<Company>().Where(x => x.Status) on store.CompanyId equals company.Id
            join order in _repository.Query<PosOrder>() on store.Id equals order.StoreId into orderGroup
            select new
            {
                Store = store,
                OrderCount = orderGroup.Count(),
                Company = company
            };
        
        if (serviceProviderId.HasValue)
            query = query.Where(x => x.Company.ServiceProviderId == serviceProviderId.Value);
        
        if (storeIds != null)
            query = query.Where(x => storeIds.Contains(x.Store.Id));

        if (companyId.HasValue)
            query = query.Where(x => x.Store.CompanyId == companyId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Store.Names.Contains(keyword) || x.Store.PhoneNums.Contains(keyword));

        query = isNormalSort
            ? query.OrderByDescending(x => x.Store.CreatedDate)
            : query.OrderByDescending(x => x.OrderCount).ThenBy(x => x.Store.CreatedDate);

        var result = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        var stores = result.Select(x => new CompanyStoreDto
        {
            Id = x.Store.Id,
            CompanyId = x.Store.CompanyId,
            ServiceProviderId = x.Company.ServiceProviderId,
            Names = x.Store.Names,
            Description = x.Store.Description,
            Status = x.Store.Status,
            PhoneNums = x.Store.PhoneNums,
            Logo = x.Store.Logo,
            Address = x.Store.Address,
            Latitude = x.Store.Latitude,
            Longitude = x.Store.Longitude,
            Link = x.Store.Link,
            AppId = x.Store.AppId,
            PosName = x.Store.PosName,
            PosId = x.Store.PosId,
            IsLink = x.Store.IsLink,
            TimePeriod = x.Store.TimePeriod,
            CreatedBy = x.Store.CreatedBy,
            CreatedDate = x.Store.CreatedDate,
            LastModifiedBy = x.Store.LastModifiedBy,
            LastModifiedDate = x.Store.LastModifiedDate,
            Count = x.OrderCount
        }).ToList();

        return stores;
    }
    
    public async Task<List<StoreUser>> GetPosStoreUsersByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
        return await _repository.Query<StoreUser>().Where(x => x.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddPosAgentsAsync(List<PosAgent> agents, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(agents, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CompanyStore> GetPosStoreByAgentIdAsync(int agentId, CancellationToken cancellationToken = default)
    {
        var query = from agent in _repository.Query<Agent>()
            join posAgent in _repository.Query<PosAgent>() on agent.Id equals posAgent.AgentId
            join store in _repository.Query<CompanyStore>() on posAgent.StoreId equals store.Id
            where agent.Id == agentId
            select store;
        
        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<List<CompanyStore>> GetPosStoresByAgentIdsAsync(List<int> agentIds, CancellationToken cancellationToken = default)
    {
        var query = from agent in _repository.Query<Agent>()
            join posAgent in _repository.Query<PosAgent>() on agent.Id equals posAgent.AgentId
            join store in _repository.Query<CompanyStore>() on posAgent.StoreId equals store.Id
            where agentIds.Contains(agent.Id)
                select store;
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PosAgent>> GetPosAgentsAsync(List<int> storeIds = null, int? agentId = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<PosAgent>();

        if (storeIds != null && storeIds.Count != 0)
            query = query.Where(x => storeIds.Contains(x.StoreId));

        if (agentId.HasValue)
            query = query.Where(x => x.AgentId == agentId.Value);
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoreUser> GetPosStoreUsersByUserIdAndAssistantIdAsync(List<int> assistantIds, int userId, CancellationToken cancellationToken = default)
    {
        var query = from assistant in _repository.Query<Domain.AISpeechAssistant.AiSpeechAssistant>().Where(x => assistantIds.Contains(x.Id))
            join agentAssistant in _repository.Query<AgentAssistant>() on assistant.Id equals agentAssistant.AssistantId
            join posAgent in _repository.Query<PosAgent>() on agentAssistant.AgentId equals posAgent.AgentId
            join posStoreUser in _repository.Query<StoreUser>() on posAgent.StoreId equals posStoreUser.StoreId
            where posStoreUser.UserId == userId
            select posStoreUser;

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PosAgent>> GetPosAgentByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
        var query = from storeUsers in _repository.Query<StoreUser>()
            join posAgent in _repository.Query<PosAgent>() on storeUsers.StoreId equals posAgent.StoreId
            where storeUsers.UserId == userId
            select posAgent;

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeletePosAgentsByAgentIdsAsync(List<int> agentIds, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        var posAgents = await _repository.Query<PosAgent>().Where(x => agentIds.Contains(x.AgentId)).ToListAsync(cancellationToken).ConfigureAwait(false);

        await _repository.DeleteAllAsync(posAgents, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<ServiceProvider>> GetServiceProviderByIdAsync(int? serviceProviderId = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<ServiceProvider>();

        if (serviceProviderId.HasValue)
        {
            query = query.Where(x => x.Id == serviceProviderId.Value);
        }

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<(CompanyStore Store, Agent Agent)>> GetStoresAndAgentsAsync(int? serviceProviderId = null, CancellationToken cancellationToken = default)
    {
        var query = from company in _repository.Query<Company>().Where(x => !serviceProviderId.HasValue || x.ServiceProviderId == serviceProviderId.Value)
            join store in _repository.Query<CompanyStore>() on company.Id equals store.CompanyId
            join posAgent in _repository.Query<PosAgent>() on store.Id equals posAgent.StoreId into posAgentGroups
            from posAgent in posAgentGroups.DefaultIfEmpty()
            join agent in _repository.Query<Agent>().Where(x => x.IsDisplay) on posAgent.AgentId equals agent.Id into agentGroups
            from agent in agentGroups.DefaultIfEmpty()
            select new { store, agent };
        
        var result = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        
        return result.Select(x => (x.store, x.agent)).ToList();
    }

    public async Task<List<SimpleStoreAgentDto>> GetSimpleStoreAgentsAsync(int? serviceProviderId = null, CancellationToken cancellationToken = default)
    {
        var query = from company in _repository.Query<Company>()
            join store in _repository.Query<CompanyStore>() on company.Id equals store.CompanyId
            join posAgent in _repository.Query<PosAgent>() on store.Id equals posAgent.StoreId
            join agent in _repository.Query<Agent>().Where(x => x.IsDisplay) on posAgent.AgentId equals agent.Id
            where !serviceProviderId.HasValue || company.ServiceProviderId == serviceProviderId.Value
            select new SimpleStoreAgentDto
            {
                StoreId = store.Id,
                AgentId = agent.Id
            };
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<CompanyStore>> GetAllStoresAsync(int? serviceProviderId = null, CancellationToken cancellationToken = default)
    {
        var query = from company in _repository.Query<Company>().Where(x => !serviceProviderId.HasValue || x.ServiceProviderId == serviceProviderId.Value)
            join store in _repository.Query<CompanyStore>() on company.Id equals store.CompanyId
            select store;
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PosAgent> GetPosAgentByAgentIdAsync(int agentId, CancellationToken cancellationToken = default)
    {
        return await _repository.Query<PosAgent>().Where(x => x.AgentId == agentId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<(PosCategory, PosProduct)>> GetPosCategoryAndProductsAsync(int storeId, CancellationToken cancellationToken)
    {
        var query = from category in _repository.Query<PosCategory>().Where(x => x.StoreId == storeId)
            join product in _repository.Query<PosProduct>().Where(x => x.StoreId == storeId) on category.Id equals product.CategoryId
            select new { category, product };
        
        var result = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        
        return result.Select(x => (x.category, x.product)).ToList();
    }
}