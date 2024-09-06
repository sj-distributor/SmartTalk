using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.Restaurants;

public interface IRestaurantDataProvider : IScopedDependency
{
    Task AddRestaurantMenuItemsAsync(List<RestaurantMenuItem> menuItems, bool forceSave = true, CancellationToken cancellationToken = default);

    Task<Restaurant> GetRestaurantByNameAsync(string name, CancellationToken cancellationToken);
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

    public async Task AddRestaurantMenuItemsAsync(List<RestaurantMenuItem> menuItems, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(menuItems, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Restaurant> GetRestaurantByNameAsync(string name, CancellationToken cancellationToken)
    {
        return await _repository.Query<Restaurant>(x => x.Name == name).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}