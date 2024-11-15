using System.Security.Cryptography;
using System.Text;
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
    
    [Fact]
    public void Test()
    {
        var a = GenerateRandomPassword(9);
        var c = ToSha256(a);
    }

    public static string GenerateRandomPassword(int length)
    {
        const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowercase = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string specialChars = "!@#$%^&*()_-+=<>?";

// 合并所有可用字符
        string allCharacters = uppercase + lowercase + digits + specialChars;

        StringBuilder password = new StringBuilder();

        Random random = new Random();

        for (int i = 0; i < length; i++)
        {
// 从可用字符中随机选择一个字符
            char nextChar = allCharacters[random.Next(allCharacters.Length)];
            password.Append(nextChar);
        }

        return password.ToString();
    }

    public static string ToSha256( string input)
    {
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(input)));
    }
}