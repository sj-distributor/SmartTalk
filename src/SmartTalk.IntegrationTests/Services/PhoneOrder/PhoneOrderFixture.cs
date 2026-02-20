using Autofac;
using Xunit;
using Shouldly;
using NSubstitute;
using Mediator.Net;
using SmartTalk.Core.Data;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.Account;
using SmartTalk.Messages.Enums.Account;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Domain.Restaurants;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;
using SmartTalk.IntegrationTests.TestBaseClasses;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.Agent;

namespace SmartTalk.IntegrationTests.Services.PhoneOrder;

public class PhoneOrderFixture : PhoneOrderFixtureBase
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ShouldGetPhoneOrderRecords(int agentId)
    {
        var restaurants = new List<Restaurant>
        {
            new ()
            {
                Id = 1,
                Name = RestaurantStore.MoonHouse,
            },
            new ()
            {
                Id = 2,
                Name = RestaurantStore.JiangNanChun,
            },            new ()
            {
                Id = 3,
                Name = RestaurantStore.XiangTanRenJia,
            }
        };

        var agents = new List<Agent>
        {
            new ()
            {
                Id = 1,
                RelateId = 1,
                Type = AgentType.Restaurant
            },
            new ()
            {
                Id = 2,
                RelateId = 2,
                Type = AgentType.Restaurant
            },
            new ()
            {
                Id = 3,
                RelateId = 3,
                Type = AgentType.Restaurant
            }
        };
        
        var records = new List<PhoneOrderRecord>();
    
        for (var i = 1; i <= 10; i++)
        {
            records.Add(new PhoneOrderRecord
            {
                Id = i,
                SessionId = Guid.NewGuid().ToString(),
                AgentId = i <= 2 ? 1 : i <= 5 ? 2 : 3,
                Tips = "",
                TranscriptionText = $"transcription text {i}",
                Url = $"https://restaurant{i}.com",
                LastModifiedBy = i % 2 == 0 ? 2 : null,
                Status = PhoneOrderRecordStatus.Sent,
                CreatedDate = DateTimeOffset.UtcNow
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
            await repository.InsertAllAsync(restaurants);
            await repository.InsertAllAsync(agents);
            await repository.InsertAllAsync(records);
            await repository.InsertAsync(user);
        });
    
        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            var response = await mediator.RequestAsync<GetPhoneOrderRecordsRequest, GetPhoneOrderRecordsResponse>(new GetPhoneOrderRecordsRequest { AgentId = agentId });
            
            response.ShouldNotBeNull();
            switch (agentId)
            {
                case 1:
                    response.Data.Count.ShouldBe(2);
                    break;
                case 2:
                    response.Data.Count.ShouldBe(3);
                    break;
                case 3:
                    response.Data.Count.ShouldBe(5);
                    break;
            }
        });
    }

    [Fact]
    public async Task ShouldGetOrAddPhoneOrderConversations()
    {
        var record = new PhoneOrderRecord
        {
            Id = 1,
            AgentId = 1,
            SessionId = Guid.NewGuid().ToString(),
            TranscriptionText = "hello hi hi hi",
            Url = "https://xxx.com"
        };
    
        var conversations = new List<PhoneOrderConversation>
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
                    await mediator.RequestAsync<GetPhoneOrderConversationsRequest, GetPhoneOrderConversationsResponse>(
                        new GetPhoneOrderConversationsRequest { RecordId = 1 });
    
                response.Data.Count.ShouldBe(conversations.Count);
    
                await mediator.SendAsync<AddPhoneOrderConversationsCommand, AddPhoneOrderConversationsResponse>(
                    new AddPhoneOrderConversationsCommand
                    {
                        Conversations = new List<PhoneOrderConversationDto>
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
    
                var afterAdd = await repository.Query<PhoneOrderConversation>().ToListAsync();
    
                afterAdd.ShouldNotBeNull();
                afterAdd.All(x => x.Question.Contains("早上好")).ShouldBeTrue();
                afterAdd.All(x => x.Answer.Contains("中午好")).ShouldBeTrue();
                afterAdd.Count.ShouldBe(4);
            },
            builder =>
            {
                var phoneOrderUtilService = Substitute.For<IPhoneOrderUtilService>();
    
                phoneOrderUtilService.ExtractPhoneOrderShoppingCartAsync(Arg.Any<string>(), Arg.Any<PhoneOrderRecord>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);;
                
                builder.RegisterInstance(phoneOrderUtilService);
            });
    }
    
    [Fact]
    public async Task ShouldGetPhoneOrderOrderItems()
    {
        var orderItems = new List<PhoneOrderOrderItem>
        {
            new ()
            {
                Id = 1,
                RecordId = 1,
                FoodName = "猪脚饭",
                Quantity = 1,
                Price = 10,
                Note = "不要辣椒",
                OrderType = PhoneOrderOrderType.AIOrder
            },
            new ()
            {
                Id = 2,
                RecordId = 1,
                FoodName = "白切鸡饭",
                Quantity = 1,
                Price = 15,
                Note = "不要葱姜",
                OrderType = PhoneOrderOrderType.AIOrder
            },
            new ()
            {
                Id = 3,
                RecordId = 1,
                FoodName = "筒骨粉",
                Quantity = 1,
                Price = 12,
                Note = "不要葱",
                OrderType = PhoneOrderOrderType.AIOrder
            },
            new ()
            {
                Id = 4,
                RecordId = 1,
                FoodName = "猪脚饭",
                Quantity = 1,
                Price = 10,
                Note = "不要辣椒",
                OrderType = PhoneOrderOrderType.ManualOrder
            },
            new ()
            {
                Id = 5,
                RecordId = 1,
                FoodName = "白切鸡饭",
                Quantity = 1,
                Price = 15,
                Note = "不要葱姜",
                OrderType = PhoneOrderOrderType.ManualOrder
            }
        };
        
        var record = new PhoneOrderRecord
        {
            Id = 1,
            AgentId = 1,
            SessionId = Guid.NewGuid().ToString(),
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
            var response = await mediator.RequestAsync<GetPhoneOrderOrderItemsRequest, GetPhoneOrderOrderItemsResponse>(new GetPhoneOrderOrderItemsRequest { RecordId = 1 });
            
            response.Data.ShouldNotBeNull();
            response.Data.ManualItems.Count.ShouldBe(2);
            response.Data.AIItems.Count.ShouldBe(3);
            response.Data.ManualOrderId.ShouldBe(123456889.ToString());
        });
    }
}