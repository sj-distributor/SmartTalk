using OpenCCNET;
using System.Text;
using Newtonsoft.Json;
using OpenAI.Interfaces;
using SmartTalk.Messages.Dto.WebSocket;

namespace SmartTalk.Core.Services.WebSocket;

public partial class Asterisk
{
    private async Task<List<string>> GetAiReplyAsync(IOpenAIService openAiService, HttpClient client, string question)
    {
        if (_chatCount < 3) return new List<string> { await AiReplyAsync(openAiService, client, question) };
        
        await TurnToManualCustomerServiceAsync(client, AriUrl, _aiChannel).ConfigureAwait(false);
        
        IsTurnToHuman = true;
        
        ReplyText = "唔好意思，我都系帮你转人工，即刻同你转接过去";
        return new List<string> { "https://speech-test.sjdistributor.com/data/tts/22300c78-0ab0-409f-8aa8-d6a8949c0387.wav" };
    }
    
    private List<Food> AddShoppingCartToOrder(List<FoodDetailDto> foods)
    {
        var allFoods = GenerateDefaultFoods();

        var orderFoods = new List<Food>();
        foreach (var food in foods)
        {
            var foodItem = allFoods.FirstOrDefault(x => x.CineseName.Contains(ZhConverter.HantToHans(food.FoodName)));
            
            if (foodItem == null) continue;
            
            orderFoods.Add(new Food
            {
                Id = foodItem.Id,
                Price = foodItem.Price,
                Quantity = food.Count ?? 1,
                EnglishName = foodItem.EnglishName,
                CineseName = foodItem.CineseName,
                Notes = string.IsNullOrEmpty(food.Remark) ? string.Empty : food.Remark
            });
        }

        return orderFoods;
    }
    
    private List<string> CheckOrAddToShoppingCart(List<FoodDetailDto> foods, HttpClient client)
    {
        var hasFoods = new List<FoodDetailDto>();

        foreach (var food in foods.Where(food => CheckFoodIfExist(food.FoodName)))
        {
            if (!string.IsNullOrEmpty(food.Remark))
            {
                hasFoods.Add(food);
                continue;
            }

            var existingFood = ShoppingCart.FirstOrDefault(t => t.FoodName == food.FoodName && string.IsNullOrEmpty(t.Remark));
            
            if (existingFood != null) existingFood.Count += food.Count;
            else hasFoods.Add(food);
        }

        if (hasFoods.Any()) ShoppingCart.AddRange(hasFoods);

        return new List<string>();
    }

    private bool CheckFoodIfExist(string foodName)
    {
        var menu = GenerateFoodMenu();

        return menu.Any(pair => pair.Value.Contains(ZhConverter.HantToHans(foodName)));
    }
    
    private List<string> CheckOrReduceFromShoppingCart(List<FoodDetailDto> foods)
    {
        var hasFoods = new List<FoodDetailDto>();
        var noneFoods = new List<FoodDetailDto>();
        
        foods.ForEach(x =>
        {
            if (ShoppingCart.Any(s => x.FoodName.Trim() == s.FoodName.Trim())) hasFoods.Add(x); 
            else noneFoods.Add(x);
        });
        
        ShoppingCart.ForEach(x =>
        {
            if (hasFoods.Any(s => s.FoodName.Trim() == x.FoodName.Trim()))
                x.Count += hasFoods.First(s => s.FoodName.Trim() == x.FoodName.Trim()).Count;
        });

        if (noneFoods.Count == foods.Count)
        {
            return new List<string>();
            // return new List<string> { "https://speech-test.sjdistributor.com/data/tts/1a97dd3e-d1ee-46bd-8346-aabe72b390e7.wav" };
        }

        ShoppingCart = ShoppingCart.Where(x => x.Count > 0).ToList();

        if (hasFoods.Any() && noneFoods.Any())
        {
            return new List<string>();
            // return new List<string> { "https://speech-test.sjdistributor.com/data/tts/4c40204a-f0c8-4a5c-930b-9dfac3149d55.wav" }.Concat(GetAudioUrls(ShoppingCart.Select(x => x.FoodName).ToList())).ToList();//部分菜品已经从购物车中移除，目前购物车中有 + todo
        }
    
        return new List<string>();
        // return new List<string> { "https://speech-test.sjdistributor.com/data/tts/2a04b5ae-6fe2-4e50-bec5-6f8d556cbfc7.wav" };
    }
    
    private async Task OrderAsync(List<Food> foods)
    {
        var orderItems = foods.Select(x => new 
        {
            id = 0,
            productId = x.Id,
            quantity = x.Quantity,
            originalPrice = x.Price,
            price = x.Price,
            notes = x.Notes
        }).ToList();

        var orders = new
        {
            id = 0,
            type = 9,
            isTaxFree = true,
            notes = "",
            orderItems = orderItems
        };
        
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6Ijg4MDIxNTcxNjA5NTY5MzMiLCJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9zeXN0ZW0iOiJDb21wYW55QWNjb3VudCIsIm5iZiI6MTcxOTQ2Nzk4NiwiZXhwIjoxNzgyNTM5OTg2LCJpc3MiOiJodHRwczovL2Vhc3lwb3MuY29tIiwiYXVkIjoic2luZ2xlLXBvcyJ9.fnDu2iw0xkgrCvmbg5uW2EsiGmqzUmhm2j98Yrgh6og");
        client.DefaultRequestHeaders.Add("merchantId", "8988245704442885");
        client.DefaultRequestHeaders.Add("companyId", "8987498732585989");
        client.DefaultRequestHeaders.Add("merchantStaffId", "8988245707195397");
        
        var content = new StringContent(JsonConvert.SerializeObject(orders), Encoding.UTF8, "application/json");
        Console.WriteLine(content.ReadAsStringAsync().Result);
        var response = await client.PostAsync("https://easypostestapi.yesmeal.com/api/merchant/order", content);
        var responseIsSuccessStatusCode = response.IsSuccessStatusCode;
    }
}