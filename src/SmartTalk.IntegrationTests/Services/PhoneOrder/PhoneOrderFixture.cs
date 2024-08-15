using Mediator.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.IntegrationTests.TestBaseClasses;
using SmartTalk.Messages.Commands;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Requests;
using SmartTalk.Messages.Requests.PhoneOrder;
using Xunit;

namespace SmartTalk.IntegrationTests.Services.PhoneOrder;

public class PhoneOrderFixture : PhoneOrderFixtureBase
{
    [Theory]
    [InlineData(PhoneOrderRestaurant.MoonHouse)]
    [InlineData(PhoneOrderRestaurant.JiangNanChun)]
    [InlineData(PhoneOrderRestaurant.XiangTanRenJia)]
    public async Task ShouldGetPhoneOrderRecords(PhoneOrderRestaurant restaurant)
    {
        var records = new List<PhoneOrderRecord>();

        for (var i = 1; i <= 10; i++)
        {
            records.Add(new PhoneOrderRecord
            {
                Id = i,
                SessionId = Guid.NewGuid().ToString(),
                Restaurant = i <= 2 ? PhoneOrderRestaurant.MoonHouse : i <= 5 ? PhoneOrderRestaurant.JiangNanChun : PhoneOrderRestaurant.XiangTanRenJia,
                Tips = "",
                TranscriptionText = $"transcription text {i}",
                Url = $"https://restaurant{i}.com"
            });
        }

        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            await repository.InsertAllAsync(records);
        });

        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            var response = await mediator.RequestAsync<GetPhoneOrderRecordsRequest, GetPhoneOrderRecordsResponse>(new GetPhoneOrderRecordsRequest { Restaurant = restaurant });
            
            response.ShouldNotBeNull();
            switch (restaurant)
            {
                case PhoneOrderRestaurant.MoonHouse:
                    response.Data.Count.ShouldBe(2);
                    break;
                case PhoneOrderRestaurant.JiangNanChun:
                    response.Data.Count.ShouldBe(3);
                    break;
                case PhoneOrderRestaurant.XiangTanRenJia:
                    response.Data.Count.ShouldBe(5);
                    break;
            }
        });
    }

    [Fact]
    public async Task ShouldGetOrAddPhoneOrderConversations()
    {
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
            await repository.InsertAllAsync(conversations);
        });

        await RunWithUnitOfWork<IMediator, IRepository>(async (mediator, repository) =>
        {
            var response = await mediator.RequestAsync<GetPhoneOrderConversationsRequest, GetPhoneOrderConversationsResponse>(new GetPhoneOrderConversationsRequest { RecordId = 1 });
            
            response.Data.Count.ShouldBe(conversations.Count);
            
            await mediator.SendAsync<AddPhoneOrderConversationsCommand, AddPhoneOrderConversationsResponse>(new AddPhoneOrderConversationsCommand
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
        
        await RunWithUnitOfWork<IRepository>(async repository =>
        {
            await repository.InsertAllAsync(orderItems);
        });

        await RunWithUnitOfWork<IMediator>(async mediator =>
        {
            var response = await mediator.RequestAsync<GetPhoneOrderOrderItemsRequest, GetPhoneOrderOrderItemsRessponse>(new GetPhoneOrderOrderItemsRequest { RecordId = 1 });
            
            response.Data.ShouldNotBeNull();
            response.Data.ManualItems.Count.ShouldBe(2);
            response.Data.AIItems.Count.ShouldBe(3);
        });
    }
}