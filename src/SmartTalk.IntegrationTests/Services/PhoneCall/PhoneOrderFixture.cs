using Autofac;
using Xunit;
using Shouldly;
using NSubstitute;
using Mediator.Net;
using SmartTalk.Core.Data;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Core.Domain.PhoneCall;
using SmartTalk.Messages.Enums.Account;
using SmartTalk.Core.Services.PhoneCall;
using SmartTalk.Messages.Dto.PhoneCall;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneCall;
using SmartTalk.Messages.Requests.PhoneCall;
using SmartTalk.IntegrationTests.TestBaseClasses;

namespace SmartTalk.IntegrationTests.Services.PhoneOrder;

public class PhoneOrderFixture : PhoneOrderFixtureBase
{
    [Theory]
    [InlineData(PhoneCallRestaurant.MoonHouse)]
    [InlineData(PhoneCallRestaurant.JiangNanChun)]
    [InlineData(PhoneCallRestaurant.XiangTanRenJia)]
    public async Task ShouldGetPhoneOrderRecords(PhoneCallRestaurant restaurant)
    {
        var records = new List<PhoneCallRecord>();

        for (var i = 1; i <= 10; i++)
        {
            records.Add(new PhoneCallRecord
            {
                Id = i,
                SessionId = Guid.NewGuid().ToString(),
                Restaurant = i <= 2 ? PhoneCallRestaurant.MoonHouse : i <= 5 ? PhoneCallRestaurant.JiangNanChun : PhoneCallRestaurant.XiangTanRenJia,
                Tips = "",
                TranscriptionText = $"transcription text {i}",
                Url = $"https://restaurant{i}.com",
                LastModifiedBy = i % 2 ==0 ? 2 : null
            });
        }

        var user = new UserAccount
        {
            Id = 2,
            Uuid = Guid.NewGuid(),
            UserName = "jojo",
            Password = "jojo",
            ThirdPartyUserId = Guid.NewGuid().ToString(),
            Issuer = UserAccountIssuer.Wiltechs,
            IsActive = true
        };

        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            await repository.InsertAllAsync(records);
            await repository.InsertAsync(user);
        });

        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            var response = await mediator.RequestAsync<GetPhoneCallRecordsRequest, GetPhoneCallRecordsResponse>(new GetPhoneCallRecordsRequest { Restaurant = restaurant });
            
            response.ShouldNotBeNull();
            switch (restaurant)
            {
                case PhoneCallRestaurant.MoonHouse:
                    response.Data.Count.ShouldBe(2);
                    break;
                case PhoneCallRestaurant.JiangNanChun:
                    response.Data.Count.ShouldBe(3);
                    break;
                case PhoneCallRestaurant.XiangTanRenJia:
                    response.Data.Count.ShouldBe(5);
                    break;
            }
        });
    }

    [Fact]
    public async Task ShouldGetOrAddPhoneOrderConversations()
    {
        var record = new PhoneCallRecord
        {
            Id = 1,
            SessionId = Guid.NewGuid().ToString(),
            Restaurant = PhoneCallRestaurant.MoonHouse,
            TranscriptionText = "hello hi hi hi",
            Url = "https://xxx.com"
        };

        var conversations = new List<PhoneCallConversation>
        {
            new()
            {
                Id = 1,
                RecordId = 1,
                Question = "你好",
                Answer = "hi",
                Order = 1
            },
            new()
            {
                Id = 2,
                RecordId = 1,
                Question = "你好1111",
                Answer = "hi1111",
                Order = 2
            },
            new()
            {
                Id = 3,
                RecordId = 1,
                Question = "你好222",
                Answer = "hi222",
                Order = 3
            },
        };

        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            await repository.InsertAsync(record);
            await repository.InsertAllAsync(conversations);
        });

        await RunWithUnitOfWork<IMediator, IRepository>(async (mediator, repository) =>
            {
                var response =
                    await mediator.RequestAsync<GetPhoneCallConversationsRequest, GetPhoneCallConversationsResponse>(
                        new GetPhoneCallConversationsRequest { RecordId = 1 });

                response.Data.Count.ShouldBe(conversations.Count);

                await mediator.SendAsync<AddPhoneCallConversationsCommand, AddPhoneOrderConversationsResponse>(
                    new AddPhoneCallConversationsCommand
                    {
                        Conversations = new List<PhoneCallConversationDto>
                        {
                            new()
                            {
                                RecordId = 1,
                                Question = "早上好11",
                                Answer = "中午好11",
                                Order = 1
                            },
                            new()
                            {
                                RecordId = 1,
                                Question = "早上好22",
                                Answer = "中午好22",
                                Order = 2
                            },
                            new()
                            {
                                RecordId = 1,
                                Question = "早上好33",
                                Answer = "中午好33",
                                Order = 3
                            },
                            new()
                            {
                                RecordId = 1,
                                Question = "早上好44",
                                Answer = "中午好44",
                                Order = 4
                            },
                        }
                    });

                var afterAdd = await repository.Query<PhoneCallConversation>().ToListAsync();

                afterAdd.ShouldNotBeNull();
                afterAdd.All(x => x.Question.Contains("早上好")).ShouldBeTrue();
                afterAdd.All(x => x.Answer.Contains("中午好")).ShouldBeTrue();
                afterAdd.Count.ShouldBe(4);
            },
            builder =>
            {
                var phoneOrderUtilService = Substitute.For<IPhoneCallUtilService>();

                phoneOrderUtilService.ExtractPhoneOrderShoppingCartAsync(Arg.Any<string>(), Arg.Any<PhoneCallRecord>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);;
                
                builder.RegisterInstance(phoneOrderUtilService);
            });
    }

    [Fact]
    public async Task ShouldGetPhoneOrderOrderItems()
    {
        var orderItems = new List<PhoneCallOrderItem>
        {
            new ()
            {
                Id = 1,
                RecordId = 1,
                FoodName = "猪脚饭",
                Quantity = 1,
                Price = 10,
                Note = "不要辣椒",
                CallOrderType = PhoneCallOrderType.AIOrder
            },
            new ()
            {
                Id = 2,
                RecordId = 1,
                FoodName = "白切鸡饭",
                Quantity = 1,
                Price = 15,
                Note = "不要葱姜",
                CallOrderType = PhoneCallOrderType.AIOrder
            },
            new ()
            {
                Id = 3,
                RecordId = 1,
                FoodName = "筒骨粉",
                Quantity = 1,
                Price = 12,
                Note = "不要葱",
                CallOrderType = PhoneCallOrderType.AIOrder
            },
            new ()
            {
                Id = 4,
                RecordId = 1,
                FoodName = "猪脚饭",
                Quantity = 1,
                Price = 10,
                Note = "不要辣椒",
                CallOrderType = PhoneCallOrderType.ManualOrder
            },
            new ()
            {
                Id = 5,
                RecordId = 1,
                FoodName = "白切鸡饭",
                Quantity = 1,
                Price = 15,
                Note = "不要葱姜",
                CallOrderType = PhoneCallOrderType.ManualOrder
            }
        };
        
        var record = new PhoneCallRecord
        {
            Id = 1,
            SessionId = Guid.NewGuid().ToString(),
            Restaurant = PhoneCallRestaurant.MoonHouse,
            TranscriptionText = "hello hi hi hi",
            Url = "https://xxx.com",
            ManualOrderId = 123456889,
        };
        
        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            await repository.InsertAsync(record);
            await repository.InsertAllAsync(orderItems);
        });

        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            var response = await mediator.RequestAsync<GetPhoneCallOrderItemsRequest, GetPhoneCallOrderItemsRessponse>(new GetPhoneCallOrderItemsRequest { RecordId = 1 });
            
            response.Data.ShouldNotBeNull();
            response.Data.ManualItems.Count.ShouldBe(2);
            response.Data.AIItems.Count.ShouldBe(3);
            response.Data.ManualOrderId.ShouldBe(123456889.ToString());
        });
    }
}