using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.Restaurants;

public interface IRestaurantDataProvider : IScopedDependency
{
    Task AddRestaurantAsync(Restaurant restaurant, bool forceSave = true, CancellationToken cancellationToken = default);
    
    Task AddRestaurantMenuItemsAsync(List<RestaurantMenuItem> menuItems, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<Restaurant> GetRestaurantByNameAsync(string name, CancellationToken cancellationToken);

    Task<List<RestaurantMenuItem>> GetRestaurantMenuItemsAsync(int? restaurantId = null, int? pageIndex = null, int? pageSize = null, string keyword = null, List<long> productIds = null, CancellationToken cancellationToken = default);
    
    Task DeleteRestaurantMenuItemsAsync(List<RestaurantMenuItem> menuItems, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<Restaurant> GetRestaurantByAgentIdAsync(int agentId, CancellationToken cancellationToken);

    Task<List<Restaurant>> GetRestaurantsAsync(List<int> ids = null, CancellationToken cancellationToken = default);
    
    Task<(int Count, List<RestaurantMenuItem> MenuItems)> GetRestaurantMenuItemsInPageAsync(
        int? pageIndex = null, int? pageSize = null, int? restaurantId = null, string keyword = null, CancellationToken cancellationToken = default);
}

public class RestaurantDataProvider : IRestaurantDataProvider
{
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RestaurantDataProvider(IUnitOfWork unitOfWork, IRepository repository)
    {
        _unitOfWork = unitOfWork;
        _repository = repository;
    }

    public async Task AddRestaurantAsync(Restaurant restaurant, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        var existRestaurant = await _repository.Query<Restaurant>().Where(x => x.Name == restaurant.Name).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (existRestaurant != null) throw new Exception("Restaurant already exists");
        
        await _repository.InsertAsync(restaurant, cancellationToken).ConfigureAwait(false);
        
        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddRestaurantMenuItemsAsync(List<RestaurantMenuItem> menuItems, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(menuItems, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Restaurant> GetRestaurantByNameAsync(string name, CancellationToken cancellationToken)
    {
        return await _repository.Query<Restaurant>(x => x.Name == name).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<RestaurantMenuItem>> GetRestaurantMenuItemsAsync(int? restaurantId = null, int? pageIndex = null, int? pageSize = null, string keyword = null, List<long> productIds = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.QueryNoTracking<RestaurantMenuItem>();

        if (restaurantId.HasValue)
            query = query.Where(x => x.RestaurantId == restaurantId.Value);

        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(x => x.Name.Contains(keyword));

        if (productIds is { Count: > 0 })
            query = query.Where(x => productIds.Contains(x.ProductId.Value));

        if (pageIndex.HasValue && pageSize.HasValue)
            query = query.Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteRestaurantMenuItemsAsync(
        List<RestaurantMenuItem> menuItems, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAllAsync(menuItems, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Restaurant> GetRestaurantByAgentIdAsync(int agentId, CancellationToken cancellationToken)
    {
        var query = from agent in _repository.Query<Agent>()
            join restaurant in _repository.Query<Restaurant>() on agent.RelateId equals restaurant.Id
            where agent.Id == agentId
            select restaurant;

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<Restaurant>> GetRestaurantsAsync(List<int> ids = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.QueryNoTracking<Restaurant>();

        if (ids is { Count: > 0 })
            query = query.Where(x => ids.Contains(x.Id));
        
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(int Count, List<RestaurantMenuItem> MenuItems)> GetRestaurantMenuItemsInPageAsync(
        int? pageIndex = null, int? pageSize = null, int? restaurantId = null, string keyword = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.QueryNoTracking<RestaurantMenuItem>();

        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(x => x.Name.Contains(keyword));
        
        if (restaurantId.HasValue)
            query = query.Where(x => x.RestaurantId == restaurantId.Value);

        var count = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        if (pageIndex.HasValue && pageSize.HasValue)
            query = query.Skip((pageIndex.Value - 1) * pageSize.Value).Take(pageSize.Value);
        
        var menuItems = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        return (count, menuItems);
    }
}