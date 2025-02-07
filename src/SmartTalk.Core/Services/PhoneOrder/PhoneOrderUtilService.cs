using Newtonsoft.Json;
using Serilog;
using Smarties.Messages.DTO.JustIm;
using Smarties.Messages.DTO.OpenAi;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Core.Services.RetrievalDb.VectorDb;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.Restaurant;
using SmartTalk.Messages.Dto.WebSocket;

namespace SmartTalk.Core.Services.PhoneOrder;

public interface IPhoneOrderUtilService : IScopedDependency
{
    Task ExtractPhoneOrderShoppingCartAsync(string goalTexts, PhoneOrderRecord record, CancellationToken cancellationToken);
}

public class PhoneOrderUtilService : IPhoneOrderUtilService
{
    private readonly IVectorDb _vectorDb;
    private readonly ISmartiesClient _smartiesClient;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly IRestaurantDataProvider _restaurantDataProvider;

    public PhoneOrderUtilService(IVectorDb vectorDb, ISmartiesClient smartiesClient, 
        IPhoneOrderDataProvider phoneOrderDataProvider, IRestaurantDataProvider restaurantDataProvider)
    {
        _vectorDb = vectorDb;
        _smartiesClient = smartiesClient;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _restaurantDataProvider = restaurantDataProvider;
    }

    public async Task ExtractPhoneOrderShoppingCartAsync(string goalTexts, PhoneOrderRecord record, CancellationToken cancellationToken)
    {
        var shoppingCart = await GetOrderDetailsAsync(goalTexts, cancellationToken).ConfigureAwait(false);

        shoppingCart = await GetSimilarRestaurantByRecordAsync(record, shoppingCart, cancellationToken).ConfigureAwait(false);
        
        var items = shoppingCart.FoodDetails.Select(x => new PhoneOrderOrderItem
        {
            RecordId = record.Id,
            FoodName = x.FoodName,
            Quantity = int.TryParse(x.Count, out var parsedValue) ? parsedValue : 1,
            Price = x.Price,
            Note = x.Remark
        }).ToList();

        if (items.Any())
            await _phoneOrderDataProvider.AddPhoneOrderItemAsync(items, true, cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<PhoneOrderDetailDto> GetOrderDetailsAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            return await PerformQueryAsync(query, OpenAiModel.DeepSeekReasoner, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error when GetOrderDetails with model {Model}, retrying with Gpto1Preview model.", OpenAiModel.DeepSeekChat);

            return await PerformQueryAsync(query, OpenAiModel.Gpto1Preview, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<PhoneOrderDetailDto> PerformQueryAsync(string query, OpenAiModel model, CancellationToken cancellationToken)
    {
        var completionResult = await _smartiesClient.PerformQueryAsync(new AskGptRequest
        {
            Messages = new List<CompletionsRequestMessageDto>
            {
                new()
                {
                    Role = "system",
                    Content = new CompletionsStringContent(
                        "你是一款高度理解语言的智能助手，根据所有对话提取Client的food_details和Client的client_phone_number" +
                        "--规则：1.根据全文帮我提取food_details，count是菜品的数量且为整数，如果你不清楚数量的时候，count默认为1，remark是对菜品的备注\n" +
                        "2.根据对话中Client的话为主，restaurant的话为辅，提取文中所提到的真正需要下单的food_details\n " +
                        "3.不要出现重复菜品，如果有特殊的要求请标明数量，例如我要两份粥，一份要辣，则标注一份要辣注意用json格式\n " +
                        "返回；规则：{\"client_phone_number\":\"电话号码\",\"food_details\": [{\"food_name\": \"菜品名字\",\"count\":减少的数量（负数）, \"remark\":null}]}}\n " +
                        "-样本与输出：input: Restaurant: . Client:Hi, 我可以要一個外賣嗎? Restaurant:可以啊,要什麼? Client: 我要幾個特價午餐,要一個蒙古牛,要一個蛋花湯跟這個,再要一個椒鹽排骨蛋花湯,然後再要一個魚香肉絲,不要辣的蛋花湯。" +
                        "Restaurant:可以吧。Client:然后再要一个春卷 再要一个法式柠檬柳粒。Restaurant:好的，已为您记录下来了，请问您的电话号码是多少？Client:310-443-65070。" +
                        "output:{\"client_phone_number\": \"310-443-65070\"\"food_details\": [{\"food_name\":\"蒙古牛\",\"count\":1, \"remark\":null},{\"food_name\":\"蛋花湯\",\"count\":3, \"remark\":},{\"food_name\":\"椒鹽排骨\",\"count\":1, \"remark\":null},{\"food_name\":\"魚香肉絲\",\"count\":1, \"remark\":null},{\"food_name\":\"春卷\",\"count\":1, \"remark\":null},{\"food_name\":\"法式柠檬柳粒\",\"count\":1, \"remark\":null}]}\n " +
                        "input: Restaurant: Moon house Client: Hi, may I please have a compound chicken with steamed white rice? Restaurant: Sure, 10 minutes, thank you. Client: Hold on, I'm not finished, I'm not finished Restaurant: Ok, Sir, First Sir, give me your phone number first One minute, One minute, One minute, One minute, Ok, One minute, One minute Client: Okay Restaurant: Ok, 213 Client: 590-6995 You guys want me to order something for you guys? Restaurant: 295, Rm Client: 590-2995 Restaurant: Ah, no, yeah, maybe they have an old one, so, that's why. Client: Okay, come have chicken with cream white rice Restaurant: Bye bye, okay, something else? Client: Good morning, Kidman Restaurant: Okay Client: What do you want?  An order of mongolian beef also with cream white rice please Restaurant: Client: Do you want something, honey?  No, on your plate, you want it?  Let's go to the level, that's a piece of meat.  Let me get an order of combination fried rice, please. Restaurant: Sure, Question is how many wires do we need? Client: Maverick, do you want to share a chicken chow mein with me, for later?  And a chicken chow mein, please.  So that's one compote chicken, one orange chicken, one mingolian beef, one combination rice, and one chicken chow mein, please. Restaurant: Okay, let's see how many, one or two Client: Moon house Restaurant: Tube Tuner, right? Client: Can you separate, can you put in a bag by itself, the combination rice and the mongolian beef with one steamed rice please, because that's for getting here with my daughter. Restaurant: Okay, so let me know.  Okay, so I'm going to leave it.  Okay.  Got it Client: Moon house Restaurant: I'll make it 20 minutes, OK?  Oh, I'm sorry, you want a Mangaloreng beef on a fried rice and one steamed rice separate, right?  Yes.  OK. Client: combination rice, the mongolian beans and the steamed rice separate in one bag. Restaurant: Okay, Thank you Thank you\n" +
                        "output:{\"client_phone_number\": \"590-6995\",\"food_details\":[{\"food_name\":\"compound chicken\",\"count\":1, \"remark\":null},{\"food_name\":\"orange chicken\",\"count\":1, \"remark\":null},{\"food_name\":\"mongolian beef\",\"count\":1, \"remark\":null},{\"food_name\":\"chicken chow mein\",\"count\":1, \"remark\":null},{\"food_name\":\"combination rice\",\"count\":1, \"remark\":null},{\"food_name\":\"white rice\",\"count\":2, \"remark\":null}]}")
                },
                new()
                {
                    Role = "user",
                    Content = new CompletionsStringContent($"input:{query}, output:")
                }
            },
            Model = model,
            ResponseFormat = new() { Type = "json_object" }
        }, cancellationToken).ConfigureAwait(false);
        
        return completionResult.Data.Response == null ? null : JsonConvert.DeserializeObject<PhoneOrderDetailDto>(completionResult.Data.Response);
    }

    private async Task<PhoneOrderDetailDto> GetSimilarRestaurantByRecordAsync(PhoneOrderRecord record, PhoneOrderDetailDto foods, CancellationToken cancellationToken)
    {
        var result = new PhoneOrderDetailDto { FoodDetails = new List<FoodDetailDto>() };
        var restaurant = await _restaurantDataProvider.GetRestaurantByNameAsync(record.RestaurantInfo.Name, cancellationToken).ConfigureAwait(false);

        var tasks = foods.FoodDetails.Select(async foodDetail =>
        {
            var similarFoodsResponse = await _vectorDb.GetSimilarListAsync(
                restaurant.Id.ToString(), foodDetail.FoodName, minRelevance: 0.4, cancellationToken: cancellationToken).ToListAsync(cancellationToken);

            if (similarFoodsResponse.Count == 0) return null;
            
            var payload = similarFoodsResponse.First().Item1.Payload[VectorDbStore.ReservedRestaurantPayload].ToString();
            
            if (string.IsNullOrEmpty(payload)) return null;
            
            foodDetail.FoodName = JsonConvert.DeserializeObject<RestaurantPayloadDto>(payload).Name;
            foodDetail.Price = (double)JsonConvert.DeserializeObject<RestaurantPayloadDto>(payload).Price;
            
            return foodDetail;
        }).ToList();

        var completedTasks = await Task.WhenAll(tasks);
        
        result.FoodDetails.AddRange(completedTasks.Where(fd => fd != null));

        return result;
    }
}