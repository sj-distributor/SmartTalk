using AutoMapper;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Data;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Core.Domain.VoiceAi.PosManagement;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;

namespace SmartTalk.Core.Services.VoiceAi.PosManagement;

public partial interface IPosManagementDataProvider : IScopedDependency
{
    Task<(int Count, List<PosCompany> Companies)> GetPosCompaniesAsync(
        int? pageIndex = null, int? pageSize = null, List<int> companyIds = null, string keyword = null, CancellationToken cancellationToken = default);
        
    Task<PosCompanyStore> GetPosCompanyStoreAsync(string link = null, int? id = null, CancellationToken cancellationToken = default);
    
    Task<PosCompanyStoreDto> GetPosCompanyStoreDetailAsync(int? id = null, CancellationToken cancellationToken = default);
    
    Task<List<PosCompanyStore>> GetPosCompanyStoresAsync(List<int> ids = null, List<int> companyIds = null, CancellationToken cancellationToken = default);
    
    Task AddPosCompanyStoresAsync(List<PosCompanyStore> stores, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task UpdatePosCompanyStoresAsync(List<PosCompanyStore> stores, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task DeletePosCompanyStoresAsync(List<PosCompanyStore> stores, bool forceSave = true, CancellationToken cancellationToken = default);

    Task CreatePosStoreUserAsync(List<PosStoreUser> posStoreUsers, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task DeletePosStoreUsersAsync(List<PosStoreUser> posStoreUsers, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task<List<PosStoreUserDto>> GetPosStoreUsersAsync(int storeId, CancellationToken cancellationToken = default);
    
    Task<List<PosCompanyStoreDto>> GetPosCompanyStoresWithSortingAsync(List<int> storeIds = null,
        int? companyId = null, string keyword = null, bool isNormalSort = false, CancellationToken cancellationToken = default);

    Task<List<PosStoreUser>> GetPosStoreUsersByUserIdAsync(int userId, CancellationToken cancellationToken);
}

public partial class PosManagementDataProvider : IPosManagementDataProvider
{
    private readonly IMapper _mapper;
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public PosManagementDataProvider(IMapper mapper, IRepository repository, IUnitOfWork unitOfWork)
    {
        _mapper = mapper;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<(int Count, List<PosCompany> Companies)> GetPosCompaniesAsync(
        int? pageIndex = null, int? pageSize = null, List<int> companyIds = null, string keyword = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<PosCompany>();

        if (companyIds != null && companyIds.Count != 0)
            query = query.Where(x => companyIds.Contains(x.Id));

        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(x => x.Name.Contains(keyword));

        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        if (pageIndex.HasValue && pageSize.HasValue)
            query = query.Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
        
        var companies = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        return (count, companies);
    }

    public async Task<PosCompanyStore> GetPosCompanyStoreAsync(string link = null, int? id = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<PosCompanyStore>();

        if (id.HasValue)
            query = query.Where(x => x.Id == id.Value);

        if (!string.IsNullOrEmpty(link))
            query = query.Where(x => x.Link == link);

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PosCompanyStoreDto> GetPosCompanyStoreDetailAsync(int? id = null, CancellationToken cancellationToken = default)
    {
        var query = from company in _repository.Query<PosCompany>()
            join store in _repository.Query<PosCompanyStore>() on company.Id equals store.CompanyId
            select new PosCompanyStoreDto
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
                AppSecret = store.AppSecret,
                IsLink = store.IsLink,
                PosId = store.PosId,
                PosName = store.PosName,
                TimePeriod = store.TimePeriod,
                Timezone = store.Timezone,
                CreatedBy = store.CreatedBy,
                CreatedDate = store.CreatedDate,
                LastModifiedBy = store.LastModifiedBy,
                LastModifiedDate = store.LastModifiedDate
            };

        if (id.HasValue)
            query = query.Where(x => x.Id == id.Value);

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PosCompanyStore>> GetPosCompanyStoresAsync(List<int> ids = null, List<int> companyIds = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.Query<PosCompanyStore>();

        if (ids != null && ids.Count != 0)
            query = query.Where(x => ids.Contains(x.Id));
        
        if (companyIds != null && companyIds.Count != 0)
            query = query.Where(x => companyIds.Contains(x.CompanyId));

        return await query.OrderByDescending(x => x.CreatedDate).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddPosCompanyStoresAsync(List<PosCompanyStore> stores, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(stores, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdatePosCompanyStoresAsync(List<PosCompanyStore> stores, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateAllAsync(stores, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeletePosCompanyStoresAsync(List<PosCompanyStore> stores, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAllAsync(stores, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CreatePosStoreUserAsync(List<PosStoreUser> posStoreUsers, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(posStoreUsers, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeletePosStoreUsersAsync(List<PosStoreUser> posStoreUsers, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAllAsync(posStoreUsers, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PosStoreUserDto>> GetPosStoreUsersAsync(int storeId, CancellationToken cancellationToken = default)
    {
        var query = from storeUser in _repository.Query<PosStoreUser>()
            join userAccount in _repository.Query<UserAccount>() on storeUser.UserId equals userAccount.Id into userAccounts
            from userAccount in userAccounts.DefaultIfEmpty()
            where storeUser.StoreId == storeId
            select new PosStoreUserDto()
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

    public async Task<List<PosCompanyStoreDto>> GetPosCompanyStoresWithSortingAsync(
        List<int> storeIds = null, int? companyId = null, string keyword = null, bool isNormalSort = false, CancellationToken cancellationToken = default)
    {
        var query = from store in _repository.Query<PosCompanyStore>()
            join order in _repository.Query<PosOrder>() on store.Id equals order.StoreId into orderGroup
            select new
            {
                Store = store,
                OrderCount = orderGroup.Count()
            };
        
        if (storeIds != null && storeIds.Count != 0)
            query = query.Where(x => storeIds.Contains(x.Store.Id));

        if (companyId.HasValue)
            query = query.Where(x => x.Store.CompanyId == companyId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(x => x.Store.Names.Contains(keyword) || x.Store.PhoneNums.Contains(keyword));

        query = isNormalSort
            ? query.OrderByDescending(x => x.Store.CreatedDate)
            : query.OrderByDescending(x => x.OrderCount).ThenByDescending(x => x.Store.CreatedDate);

        var result = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        var stores = result.Select(x => new PosCompanyStoreDto
        {
            Id = x.Store.Id,
            CompanyId = x.Store.CompanyId,
            Names = x.Store.Names,
            Description = x.Store.Description,
            Status = false,
            PhoneNums = x.Store.PhoneNums,
            Logo = x.Store.Logo,
            Address = x.Store.Address,
            Latitude = x.Store.Latitude,
            Longitude = x.Store.Longitude,
            Link = x.Store.Link,
            AppId = x.Store.AppId,
            AppSecret = x.Store.AppSecret,
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
    
    public async Task<List<PosStoreUser>> GetPosStoreUsersByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
        return await _repository.Query<PosStoreUser>().Where(x => x.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}