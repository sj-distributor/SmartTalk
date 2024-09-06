using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using OpenCCNET;
using SmartTalk.Core.Extensions;
using SmartTalk.Messages.Dto.WebSocket;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.WebSocket;

public partial class Asterisk
{
    public async Task<List<string>> HandleIntentAsync(PhoneOrderIntent intent, IOpenAIService openAiService, HttpClient client, string question)
    {
        switch (intent)
        {
            case PhoneOrderIntent.Chat:
                return await HandleChatIntentAsync(openAiService, client, question);
            case PhoneOrderIntent.SayHi:
                return HandleSayHiIntent();
            case PhoneOrderIntent.AskOpeningHours:
                return HandleAskOpeningHoursIntent();
            case PhoneOrderIntent.AskAddress:
                return HandleAskAddressIntent();
            case PhoneOrderIntent.AskGlutenFree:
                return GetGlutenFoods();
            case PhoneOrderIntent.AskMSG:
                return HandleAskMSGIntent();
            case PhoneOrderIntent.AskDishes:
                return await HandleAskDishesIntentAsync(openAiService, question);
            case PhoneOrderIntent.AddOrder:
                return await HandleAddOrderIntentAsync(openAiService, client, question, AriUrl);
            case PhoneOrderIntent.ReduceOrder:
                return await HandleReduceOrderIntentAsync(openAiService, client, question, AriUrl);
            case PhoneOrderIntent.AskShoppingCart:
                return HandleAskOrderIntent(client);
            case PhoneOrderIntent.TransferToHuman:
                return await HandleTransferToHumanIntentAsync(client, AriUrl);
            case PhoneOrderIntent.Order:
                return await HandleOrderIntentAsync(client, ShoppingCart);
            case PhoneOrderIntent.SayBye:
                return HandleSayByeIntent();
            default:
                return await GetAiReplyAsync(openAiService, client, question);
        }
    }

    private async Task<List<string>> HandleChatIntentAsync(IOpenAIService openAiService, HttpClient client, string question)
    {
        _chatCount++;
        return await GetAiReplyAsync(openAiService, client, question);
    }

    private List<string> HandleSayHiIntent()
    {
        var url = new List<string>
        {
            new List<string>
            {
                "https://speech-test.sjdistributor.com/data/tts/38a9c16c-0785-4607-add7-5dda93c9c6a5.wav",
                "https://speech-test.sjdistributor.com/data/tts/c9de5450-056c-4ad7-a782-a183ea6fcda4.wav"
            }.GetRandomElement()
        };
        
        ReplyText = GetReplyText(url);
        return url;
    }

    private List<string> HandleAskOpeningHoursIntent()
    {
        ReplyText = "我哋嘅營業時間係每日朝早七點到夜晚七點";
        return new List<string> { "https://speech-test.sjdistributor.com/data/tts/62f6924f-7e47-4017-8d93-c725bbd3d625.wav" };
    }

    private List<string> HandleAskAddressIntent()
    {
        ReplyText = "我哋餐厅嘅地址係兴政路110号";
        return new List<string> { "https://speech-test.sjdistributor.com/data/tts/db1678b7-87e9-4287-9d16-9981c720f315.wav" };
    }

    private List<string> HandleAskMSGIntent()
    {
        ReplyText = "放心，我哋餐厅追求健康飲食，所以系唔放味精嘅";
        return new List<string> { "https://speech-test.sjdistributor.com/data/tts/69dc3af4-e69c-4bda-bc2b-41fa560a0f5b.wav" };
    }

    private async Task<List<string>> HandleAskDishesIntentAsync(IOpenAIService openAiService, string question)
    {
        var dish = await AskDishDetailAsync(openAiService, question);
        var foodUrl = GetFoods(dish?.FoodDetails?.FirstOrDefault()?.FoodCategory ?? string.Empty, dish?.FoodDetails?.FirstOrDefault()?.FoodName ?? string.Empty);
        
        ReplyText = GetReplyText(new List<string> { foodUrl });
        return new List<string> { foodUrl };
    }

    private async Task<List<string>> HandleAddOrderIntentAsync(IOpenAIService openAiService, HttpClient client, string question, string AriUrl)
    {
        var orderUrl = new List<string>
        {
            "https://speech-test.sjdistributor.com/data/tts/207dbe77-77b7-445e-9aa7-ea21f03b01ef.wav",
            "https://speech-test.sjdistributor.com/data/tts/72f20f2b-92ff-4155-aad8-4d49a7f1a795.wav",
            "https://speech-test.sjdistributor.com/data/tts/a76ba77b-c50f-4d2b-ad43-ef3d94da69cc.wav"
        }.GetRandomElement();
        ReplyText = GetReplyText(new List<string> { orderUrl }) + "你仲需要D乜？";
        
        await PlayAudioAsync(client, AriUrl, _aiChannel, new List<string> { orderUrl, "https://speech-test.sjdistributor.com/data/tts/d20a22d9-eab7-43b5-84dd-e2b6bb8c3616.wav" }).ConfigureAwait(false);
        
        var orderDetail = await AddOrderDetailAsync(openAiService, question).ConfigureAwait(false);
        var addResult = CheckOrAddToShoppingCart(orderDetail.FoodDetails, client);
        OrderedFoods = AddShoppingCartToOrder(ShoppingCart);
        return addResult;
    }

    private async Task<List<string>> HandleReduceOrderIntentAsync(IOpenAIService openAiService, HttpClient client, string question, string AriUrl)
    {
        ReplyText = "好，幫你劃走佢";
        await PlayAudioAsync(client, AriUrl, _aiChannel, new List<string> { "https://speech-test.sjdistributor.com/data/tts/fa561dec-30d6-4540-8015-9393f63e2601.wav" });
        
        var reduceDetail = await ReduceOrderDetailAsync(openAiService, ShoppingCart, question);
        var reduceResult = CheckOrReduceFromShoppingCart(reduceDetail.FoodDetails);
        OrderedFoods = AddShoppingCartToOrder(ShoppingCart);
        return reduceResult;
    }
    
    private List<string> HandleAskOrderIntent(HttpClient client)
    {
        if (!ShoppingCart.Any())
        {
            ReplyText = "宜家你张单冇点任何嘢，话俾我知你想食D乜？";
            return new List<string> { "https://speech-test.sjdistributor.com/data/tts/b75f055c-5f6d-4849-b886-b5b0790bc1d2.wav" };
        }
        
        ReplyText = "依家你張單有：" + string.Join('、', ShoppingCart.Select(x => ZhConverter.HansToHant(x.FoodName)));
        return new List<string> { "https://speech-test.sjdistributor.com/data/tts/254f2b13-ebee-4c7a-b9ea-54f945ad3406.wav" }.Concat(GetAudioUrls(ShoppingCart.Select(x => x.FoodName).ToList()).ToList()).ToList();
    }

    private async Task<List<string>> HandleTransferToHumanIntentAsync(HttpClient client, string AriUrl)
    {
        await TurnToManualCustomerServiceAsync(client, AriUrl, _aiChannel);
        IsTurnToHuman = true;
        ReplyText = "好，依家幫你轉人工";
        return new List<string> { "https://speech-test.sjdistributor.com/data/tts/42e08704-e9d2-48a1-8a38-5b8cbb4f991f.wav" };
    }
    
    private async Task<List<string>> HandleOrderIntentAsync(HttpClient client, List<FoodDetailDto> foods)
    {
        if (!foods.Any())
        {
            ReplyText = "宜家你张单冇点任何嘢，话俾我知你想食D乜？";
            return new List<string> { "https://speech-test.sjdistributor.com/data/tts/b75f055c-5f6d-4849-b886-b5b0790bc1d2.wav" };
        }

        var orderFoods = AddShoppingCartToOrder(foods);
        
        Console.WriteLine("foods:" + orderFoods);
        
        if (orderFoods.Any(x => x != null))
        {
            ReplyText = "收到，多謝你的訂單，已經順利幫你落單了，請問還有其他需要嗎？";
            await PlayAudioAsync(client, AriUrl, _aiChannel, new List<string> { "https://speech-test.sjdistributor.com/data/tts/9898fecf-67be-400b-a9b5-a5b41611a080.wav" });
        }
        else
        {
            ReplyText = "宜家你张单冇点任何嘢，话俾我知你想食D乜?";
            await PlayAudioAsync(client, AriUrl, _aiChannel, new List<string> { "https://speech-test.sjdistributor.com/data/tts/b75f055c-5f6d-4849-b886-b5b0790bc1d2.wav" });
        }
        
        OrderedFoods = orderFoods;

        await OrderAsync(OrderedFoods).ConfigureAwait(false);

        ShoppingCart.Clear();
        
        return new List<string>();
    }

    private List<string> HandleSayByeIntent()
    {
        ReplyText = "好的，一陣見，拜拜";
        return new List<string> { "https://speech-test.sjdistributor.com/data/tts/9235ff4a-0366-476c-81df-3df04692d669.wav" };
    }

}