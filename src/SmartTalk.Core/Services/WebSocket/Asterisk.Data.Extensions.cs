using SmartTalk.Core.Extensions;
using ZhConverter = OpenCCNET.ZhConverter;

namespace SmartTalk.Core.Services.WebSocket;

public partial class Asterisk
{
    private List<string> GetGlutenFoods()
    {
        if (!ShoppingCart.Any())
        {
            ReplyText = "除左菠蘿油同埋面食其他都系gluten free";
            return new List<string> { "https://speech-test.sjdistributor.com/data/tts/1f8320f7-62be-4737-b25a-9f6b39d00059.wav" };
        }
        
        var glutenFoods = GetGlutenFoodsFromMenu(ShoppingCart.Select(x => x.FoodName).ToList());
        ReplyText = "你張單上面嘅" + string.Join(',', glutenFoods.foods.Select(ZhConverter.HansToHant)) + "系含有gluten嘅";
        return new List<string>
                { "https://speech-test.sjdistributor.com/data/tts/9fa4df46-8b45-4d0e-88d9-44c361e86627.wav" }
                .Concat(glutenFoods.urls).Concat(new List<string>
                { "https://speech-test.sjdistributor.com/data/tts/f7940cf7-d94a-48bf-9d9f-6c5a2ee21f8f.wav" }).ToList();
    }
    
    private (List<string> foods, List<string> urls) GetGlutenFoodsFromMenu(List<string> foodNames)
    {
        var foods = new List<string>();
        var audioUrls = new List<string>();
        
        foreach (var foodName in foodNames)
        {
            if (!GenerateGlutenFoods().TryGetValue(foodName, out var isGluten) || !isGluten) continue;
            if (GenerateFoodAudios().TryGetValue(foodName, out var fileUrl))
            {
                foods.Add(foodName);
                audioUrls.Add(fileUrl);
            }
        }
        return (foods, audioUrls);
    }
    
    private static string GetFoods(string category, string foodName)
    {
        Console.WriteLine($"category:{category}, foodName{foodName}");
        if (!string.IsNullOrEmpty(category) && string.IsNullOrEmpty(foodName)) return GetFoodCategoryMenu(category);

        return new List<string>
        {
            "https://speech-test.sjdistributor.com/data/tts/644786dd-4dd1-40e3-82d0-0fa731ec59f2.wav",
            "https://speech-test.sjdistributor.com/data/tts/a74ebdd5-4767-44cd-8602-b7f142aa6976.wav",
            "https://speech-test.sjdistributor.com/data/tts/0c3dfe24-e166-4a27-a804-f39a6c75a4e6.wav"
        }.GetRandomElement();
    }
    
    private static string GetFoodCategoryMenu(string category)
    {
        Console.WriteLine($"GetFoodCategoryMenu: {category}");

        return category switch
        {
            "小吃" or "小食" or "小食" => new List<string>
            {
                "https://speech-test.sjdistributor.com/data/tts/e6d34cc8-66f3-4a99-a273-ec18dc5b2c02.wav",
                "https://speech-test.sjdistributor.com/data/tts/1cd7a460-bec0-46df-8a56-3ed652d66691.wav",
                "https://speech-test.sjdistributor.com/data/tts/2dadb08d-3b60-4126-a043-451eb0f3ff1c.wav"
            }.GetRandomElement(),
            "粥" => new List<string>
            {
                "https://speech-test.sjdistributor.com/data/tts/f6323baf-22aa-481b-a9d0-9292ed0132e6.wav",
                "https://speech-test.sjdistributor.com/data/tts/b6e20842-2ff5-4327-aea0-814da7afd009.wav",
                "https://speech-test.sjdistributor.com/data/tts/a0ee9fef-6c0a-4910-b5c2-924e1a0b923b.wav"
            }.GetRandomElement(),
            "粉面" => new List<string>
            {
                "https://speech-test.sjdistributor.com/data/tts/101ff31b-d5d4-4845-8b16-f2c43bcbae47.wav",
                "https://speech-test.sjdistributor.com/data/tts/f88956d3-b41c-4d8c-b9bd-8357fab82c96.wav",
                "https://speech-test.sjdistributor.com/data/tts/1ca2a5fe-5ec2-47dc-b36d-88ee76d443f5.wav"
            }.GetRandomElement(),
            "飯" or "饭" => new List<string>
            {
                "https://speech-test.sjdistributor.com/data/tts/b1163910-2ee1-430d-b2cf-d85a4221d101.wav",
                "https://speech-test.sjdistributor.com/data/tts/4854f346-a161-4eb2-86cd-784a197dfa14.wav",
                "https://speech-test.sjdistributor.com/data/tts/ba270fc2-2679-44fb-85a1-3318ea68db0d.wav"
            }.GetRandomElement(),
            "饮料" or "飲料" => new List<string>
            {
                "https://speech-test.sjdistributor.com/data/tts/c014d7e7-9920-4843-b38e-b2dcc949bb6d.wav",
                "https://speech-test.sjdistributor.com/data/tts/24186ceb-c040-4bbf-89fb-9fc8ef091d0c.wav"
            }.GetRandomElement(),
            _ => new List<string>
            {
                "https://speech-test.sjdistributor.com/data/tts/85133179-ba76-4530-aa92-cae27d5154e2.wav",
                "https://speech-test.sjdistributor.com/data/tts/f2e78f1e-9a96-4806-85e4-c156721d8af1.wav"
            }.GetRandomElement()
        };
    }
    
    private string GetReplyText(List<string> replyUrls)
    {
        var texts = new List<string>();
        
        foreach (var url in replyUrls)
        {
            if (GenerateReplyTexts().TryGetValue(url, out var replyText))  texts.Add(replyText); 
        }
        
        return (string.IsNullOrEmpty(texts.FirstOrDefault()) ? "唔好意思，暫時識別唔到" : texts.FirstOrDefault()) ?? "唔好意思，暫時識別唔到";
    }
    
    private List<string> GetAudioUrls(List<string> foodNames)
    {
        var audioUrls = new List<string>();
        foreach (var foodName in foodNames)
        {
            if (GenerateFoodAudios().TryGetValue(foodName, out var fileUrl))  audioUrls.Add(fileUrl); 
        }
        return audioUrls;
    }
}