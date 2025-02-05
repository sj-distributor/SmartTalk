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

    Task<List<RestaurantMenuItem>> GetRestaurantMenuItemsAsync(int? restaurantId = null, CancellationToken cancellationToken = default);
    
    Task DeleteRestaurantMenuItemsAsync(List<RestaurantMenuItem> menuItems, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<Restaurant> GetRestaurantByAgentIdAsync(int agentId, CancellationToken cancellationToken);
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

    public async Task<List<RestaurantMenuItem>> GetRestaurantMenuItemsAsync(int? restaurantId = null, CancellationToken cancellationToken = default)
    {
        var query = _repository.QueryNoTracking<RestaurantMenuItem>();

        if (restaurantId.HasValue)
            query = query.Where(x => x.RestaurantId == restaurantId.Value);
        
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
}