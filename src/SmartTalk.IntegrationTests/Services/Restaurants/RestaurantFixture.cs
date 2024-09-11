using Xunit;
using Autofac;
using Shouldly;
using NSubstitute;
using Mediator.Net;
using SmartTalk.Core.Data;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Messages.Commands.Restaurants;
using SmartTalk.IntegrationTests.TestBaseClasses;
using SmartTalk.Core.Services.RetrievalDb.VectorDb;

namespace SmartTalk.IntegrationTests.Services.Restaurants;

public class RestaurantFixture : RestaurantFixtureBase
{
    [Fact]
    public async Task ShouldAddRestaurant()
    {
        await Run<IRepository>(async repository =>
        {
            var restaurants = await repository.Query<Restaurant>().ToListAsync();
            restaurants.Count.ShouldBe(0);
        });
        
        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            await mediator.SendAsync(new AddRestaurantCommand { RestaurantName = "江南春" }).ConfigureAwait(false);
        }, builder =>
        {
            var vectorDb = Substitute.For<IVectorDb>();
            builder.RegisterInstance(vectorDb);
        });
        
        await Run<IRepository>(async repository =>
        {
            var restaurants = await repository.Query<Restaurant>().ToListAsync();
            restaurants.Count.ShouldBe(1);
        });
    }
    
    [Fact]
    public async Task ShouldNotAddRestaurantWhenExist()
    {
        var shouldThrowException = false;
        
        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            var restaurants = await repository.Query<Restaurant>().ToListAsync();
            restaurants.Count.ShouldBe(0);
            
            await repository.InsertAsync(new Restaurant { Name = "江南春" });
        });
        
        try
        {
            await RunWithUnitOfWork<IMediator>(async mediator =>
            {
                await mediator.SendAsync(new AddRestaurantCommand { RestaurantName = "江南春" }).ConfigureAwait(false);
            }, builder =>
            {
                var vectorDb = Substitute.For<IVectorDb>();
                builder.RegisterInstance(vectorDb);
            });
        }
        catch (Exception e)
        {
            shouldThrowException = true;
        }
        
        shouldThrowException.ShouldBeTrue();
        
        await Run<IRepository>(async repository =>
        {
            var restaurants = await repository.Query<Restaurant>().ToListAsync();
            restaurants.Count.ShouldBe(1);
        });
    }
}