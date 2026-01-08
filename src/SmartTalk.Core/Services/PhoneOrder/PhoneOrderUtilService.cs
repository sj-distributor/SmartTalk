using AutoMapper;
using Mediator.Net;
using Newtonsoft.Json;
using OpenAI.Chat;
using Serilog;
using Smarties.Messages.DTO.OpenAi;
using Smarties.Messages.Enums.OpenAi;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.Printer;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Caching.Redis;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Printer;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Core.Services.RetrievalDb.VectorDb;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Dto.Restaurant;
using SmartTalk.Messages.Dto.WebSocket;
using SmartTalk.Messages.Enums.Caching;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.Pos;
using SmartTalk.Messages.Enums.Printer;

namespace SmartTalk.Core.Services.PhoneOrder;

public interface IPhoneOrderUtilService : IScopedDependency
{
    Task ExtractPhoneOrderShoppingCartAsync(string goalTexts, PhoneOrderRecord record, CancellationToken cancellationToken);

    Task GenerateAiDraftAsync(PhoneOrderRecord record, Agent agent, CancellationToken cancellationToken);
}

public class PhoneOrderUtilService : IPhoneOrderUtilService
{
    private readonly IMapper _mapper;
    private readonly IVectorDb _vectorDb;
    private readonly IPosService _posService;
    private readonly OpenAiSettings _openAiSettings;
    private readonly ISmartiesClient _smartiesClient;
    private readonly IPosDataProvider _posDataProvider;
    private readonly IRedisSafeRunner _redisSafeRunner;
    private readonly IPrinterDataProvider _printerDataProvider;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly IRestaurantDataProvider _restaurantDataProvider;
    private readonly ISmartTalkBackgroundJobClient _smartTalkBackgroundJobClient;
    private readonly IAiSpeechAssistantDataProvider _aiiSpeechAssistantDataProvider;

    public PhoneOrderUtilService(IMapper mapper, IVectorDb vectorDb, IPosService posService, ISmartiesClient smartiesClient,
        IPosDataProvider posDataProvider, IPhoneOrderDataProvider phoneOrderDataProvider, IRedisSafeRunner redisSafeRunner, IRestaurantDataProvider restaurantDataProvider, ISmartTalkBackgroundJobClient smartTalkBackgroundJobClient, IAiSpeechAssistantDataProvider aiiSpeechAssistantDataProvider, OpenAiSettings openAiSettings, IPrinterDataProvider printerDataProvider)
    {
        _mapper = mapper;
        _vectorDb = vectorDb;
        _posService = posService;
        _smartiesClient = smartiesClient;
        _openAiSettings = openAiSettings;
        _posDataProvider = posDataProvider;
        _redisSafeRunner = redisSafeRunner;
        _printerDataProvider = printerDataProvider;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _restaurantDataProvider = restaurantDataProvider;
        _smartTalkBackgroundJobClient = smartTalkBackgroundJobClient;
        _aiiSpeechAssistantDataProvider = aiiSpeechAssistantDataProvider;
    }

    public async Task ExtractPhoneOrderShoppingCartAsync(string goalTexts, PhoneOrderRecord record, CancellationToken cancellationToken)
    {
        try
        {
            if (record.Scenario != DialogueScenarios.Order) return;
            
            var shoppingCart = await GetOrderDetailsAsync(goalTexts, cancellationToken).ConfigureAwait(false);
            
            var (assistant, agent) = await _aiiSpeechAssistantDataProvider.GetAgentAndAiSpeechAssistantAsync(
                record.AgentId, record.AssistantId, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            Log.Information("Get ai speech assistant: {@Assistant} and agent: {@Agent} by agentId: {AgentId}, assistantId: {AssistantId}", assistant, agent, record.AgentId, record.AssistantId);

            if (!record.AssistantId.HasValue) assistant = null;

            if (assistant is not { IsAutoGenerateOrder: true }) return;
            
            var order = await MatchSimilarProductsAsync(record, shoppingCart, cancellationToken).ConfigureAwait(false);
            
            if (assistant is { IsAllowOrderPush: true })
            {
                Log.Information("Allow order push...");
                
                switch (agent.Type)
                {
                    case AgentType.Sales:
                        await HandleSalesOrderAsync(cancellationToken).ConfigureAwait(false);
                        break;
                    default:
                        Log.Information("Ready push order: {@order}", order);
                        await HandlePosOrderAsync(order, cancellationToken).ConfigureAwait(false);
                        break;
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("Match similar items failed: {@Exception}", e);
        }
    }
    
    public async Task<PhoneOrderDetailDto> GetOrderDetailsAsync(string query, CancellationToken cancellationToken)
    {
        var completionResult = await _smartiesClient.PerformQueryAsync( new AskGptRequest
        {
            Messages = new List<CompletionsRequestMessageDto>
            {
                new () {
                    Role = "system",
                    Content = new CompletionsStringContent("你是一款高度理解语言的智能助手，根据所有对话提取Client的food_details。" +
                                       "--规则：" +
                                       "1.根据全文帮我提取food_details，count是菜品的数量且为整数，如果你不清楚数量的时候，count默认为1，remark是对菜品的备注" +
                                       "2.根据对话中Client的话为主提取food_details和 type (0: 自取订单，1：配送、外卖订单)" +
                                       "3.不要出现重复菜品，如果有特殊的要求请标明数量，例如我要两份粥，一份要辣，则标注一份要辣" +
                                       "注意用json格式返回；规则：{\"food_details\": [{\"food_name\": \"菜品名字\",\"count\": -1, \"remark\":null}], \"type\": 0}" +
                                       "-样本与输出：" +
                                       "input: Restaurant: . Client:Hi, 我可以要一個外賣嗎? Restaurant:可以啊,要什麼? Client: 我要幾個特價午餐,要一個蒙古牛,要一個蛋花湯跟這個,再要一個椒鹽排骨蛋花湯,然後再要一個魚香肉絲,不要辣的蛋花湯。Restaurant:可以吧。Client:然后再要一个春卷 再要一个法式柠檬柳粒。Client: 30分钟后我自己来拿。 out:{\"food_details\": [{\"food_name\":\"蒙古牛\",\"count\":1, \"remark\":null},{\"food_name\":\"蛋花湯\",\"count\":3, \"remark\":null},{\"food_name\":\"椒鹽排骨\",\"count\":1, \"remark\":null},{\"food_name\":\"魚香肉絲\",\"count\":1, \"remark\":null},{\"food_name\":\"春卷\",\"count\":1, \"remark\":null},{\"food_name\":\"法式柠檬柳粒\",\"count\":1, \"remark\":null}], \"type\": 0}" +
                                       "input: Restaurant: Moon house Client: Hi, may I please have a compound chicken with steamed white rice? Restaurant: Sure, 10 minutes, thank you. Client: Hold on, I'm not finished, I'm not finished Restaurant: Ok, Sir, First Sir, give me your phone number first One minute, One minute, One minute, One minute, Ok, One minute, One minute Client: Okay Restaurant: Ok, 213 Client: 590-6995 You guys want me to order something for you guys? Restaurant: 295, Rm Client: 590-2995 Restaurant: Ah, no, yeah, maybe they have an old one, so, that's why. Client: Okay, come have chicken with cream white rice Restaurant: Bye bye, okay, something else? Client: Good morning, Kidman Restaurant: Okay Client: What do you want?  An order of mongolian beef also with cream white rice please Restaurant: Client: Do you want something, honey?  No, on your plate, you want it?  Let's go to the level, that's a piece of meat.  Let me get an order of combination fried rice, please. Restaurant: Sure, Question is how many wires do we need? Client: Maverick, do you want to share a chicken chow mein with me, for later?  And a chicken chow mein, please.  So that's one compote chicken, one orange chicken, one mingolian beef, one combination rice, and one chicken chow mein, please. Restaurant: Okay, let's see how many, one or two Client: Moon house Restaurant: Tube Tuner, right? Client: Can you separate, can you put in a bag by itself, the combination rice and the mongolian beef with one steamed rice please, because that's for getting here with my daughter. Restaurant: Okay, so let me know.  Okay, so I'm going to leave it.  Okay.  Got it Client: Moon house Restaurant: I'll make it 20 minutes, OK?  Oh, I'm sorry, you want a Mangaloreng beef on a fried rice and one steamed rice separate, right?  Yes.  OK. Client: combination rice, the mongolian beans and the steamed rice separate in one bag. Restaurant: Okay. Client: Please deliver this to Beijing Road.Thank you. out:{\"food_details\":[{\"food_name\":\"compound chicken\",\"count\":1, \"remark\":null},{\"food_name\":\"orange chicken\",\"count\":1, \"remark\":null},{\"food_name\":\"mongolian beef\",\"count\":1, \"remark\":null},{\"food_name\":\"chicken chow mein\",\"count\":1, \"remark\":null},{\"food_name\":\"combination rice\",\"count\":1, \"remark\":null},{\"food_name\":\"white rice\",\"count\":2, \"remark\":null}], \"type\": 0}"
                                       )
                    
                },
                new ()
                {
                    Role = "user",
                    Content = new CompletionsStringContent($"input:{query}, output:")
                }
            },
            Model = OpenAiModel.Gpt4o,
            ResponseFormat = new () { Type = "json_object" }
        }, cancellationToken).ConfigureAwait(false);
        
        var result = completionResult.Data.Response == null ? null : JsonConvert.DeserializeObject<PhoneOrderDetailDto>(completionResult.Data.Response);
        
        Log.Information("Food extract result: {@Result}", result);

        return result;
    }

    public async Task GenerateAiDraftAsync(PhoneOrderRecord record, Agent agent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(record.TranscriptionText) || agent == null) return;
        
        var client = new ChatClient("gpt-4.1", _openAiSettings.ApiKey);
        var systemPrompt = "";
                    
        var store = await _posDataProvider.GetPosStoreByAgentIdAsync(agent.Id, cancellationToken).ConfigureAwait(false);
        var storePrintDateString = ""; 

        if (!string.IsNullOrEmpty(store.Timezone))
            storePrintDateString = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(store.Timezone)).ToString("yyyy-MM-dd HH:mm:ss");    
        else
            storePrintDateString = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");
                  
        if (record.Scenario is DialogueScenarios.Reservation)
        {
            systemPrompt =
                "你是一名餐廳電話預約資訊分析助手。" +
                $"系統當前日期與時間為：{storePrintDateString}（格式：yyyy-MM-dd HH:mm）。" +
                "所有相對日期與時間（例如：明天、後天、今晚、下週五）都必須**以此時間為唯一基準**進行推算。\n" +
                "請從下面的顧客與餐廳之間的完整對話內容中，提取所有**已確認**的餐廳預約資訊。" +
                "本任務僅用於結構化電話預約資料，請嚴格依照指定字段輸出。\n" +
                "你需要識別並提取以下資訊：" +
                "1. 預約日期，" +
                "2. 名字，" +
                "3. 電話，" +
                "4. 人數，" +
                "2. 時間，" +
                "5. 備註。（例如：包廂、靠窗、生日、過敏需求等）\n" +
                "若對話中出現多個可能的日期或時間，請僅選擇**最終已確認**的那一個；" +
                "若僅為詢問、討論或未明確確認，請將對應欄位設為空字符串。\n" +
                "輸出格式規則（非常重要）：\n" +
                "1. **不要輸出任何大括號 `{` 或 `}`**。\n" +
                "2. 請僅輸出以下字段本身，作為鍵值對，每行一個字段。\n" +
                "3. 字段名稱必須完全一致，字段順序必須固定如下：\n" +
                " 預約日期, 名字, 電話, 人數, 時間, 備註。\n" +
                "字段格式要求：" +
                "預約日期（yyyy-MM-dd，可空字符串）、" +
                "名字（字符串，可空字符串）、" +
                "電話（字符串，可空字符串）、" +
                "人數（整數，可為 null）、" +
                "時間（HH:mm，可空字符串）、" +
                "備註（字符串，可空字符串）。\n" +
                "正確輸出範例（注意：沒有大括號）：\n" +
                "預約日期: 2025-08-20\n" +
                "名字: 王先生\n" +
                "電話: 4088888888\n" +
                "人數: 4\n" +
                "時間: 18:30\n" +
                "備註: 需要包廂，並準備生日蠟燭。\n" +
                "規則與注意事項：\n" +
                "1. 不得輸出任何說明文字、註解、標題或多餘內容。\n" +
                "2. 預約日期 與 時間 必須分開填寫，不得合併。\n" +
                "3. 日期使用 yyyy-MM-dd；時間使用 24 小時制 HH:mm。\n" +
                "4. 若某欄位未被明確確認，請設為空字符串（人數 為 null）。\n" +
                "5. 名字 僅在顧客主動提供或可明確識別時填寫。\n" +
                "6. 備註 僅保留顧客明確提出的內容，不得推斷。\n" +
                "7. 若整段對話中沒有任何**已確認**的預約行為，請輸出以下內容（仍然不要大括號）：\n" +
                "預約日期: \n" +
                "名字: \n" +
                "電話: \n" +
                "人數: \n" +
                "時間: \n" +
                "備註: \n" +
                "8. 若出現相對日期或時間，且根據系統當前時間仍無法唯一確定，請將該欄位留空。\n" +
                "請務必準確、完整地提取所有**已確認**的預約資訊。";
        }
        else if (record.Scenario is DialogueScenarios.ThirdPartyOrderNotification or DialogueScenarios.InformationNotification)
        {
            systemPrompt =
                "你是一名專業的電話錄音分析員，負責根據電話錄音中可識別的語音內容，準確理解通話中所傳達的「通知事項」，並撰寫精簡明確的「通知摘要」。\n" +
                "請嚴格遵守以下規則：\n" +
                "1. 僅根據錄音中實際可識別的內容提取通知，不得臆測、補充或編造未出現的資訊。\n" +
                "2. 僅在通話中出現明確的通知行為時（例如：營業時間變更、臨時停業、服務異常、延遲、取消、活動公告等）才進行摘要。\n" +
                "3. 通知摘要需在不遺漏關鍵資訊的前提下進行精簡，避免冗餘敘述與口語重複。\n" +
                "4. 若錄音為空、無有效語音、語音模糊、僅有雜音，或通話中未出現任何可識別的通知內容，仍必須輸出通知摘要。\n" +
                "5. 當無法識別任何有效通知時，請在通知摘要中明確填寫：「未識別到有效通知內容」。\n" +
                "【輸出格式規則（非常重要）】\n" +
                "1. 嚴禁輸出任何形式的大括號「{」或「}」。\n" +
                "2. 僅輸出一行，格式必須完全如下：\n" +
                " 通知摘要: <摘要文字>\n" +
                "3. 除「通知摘要」一行外，不得輸出任何其他文字、說明、標題或換行。\n" +
                "4. 若無法產生有效通知摘要，請直接輸出：\n" +
                "通知摘要: 未識別到有效通知內容\n" +
                "【正確輸出範例（僅作格式參考）】\n" +
                "通知摘要: 今日晚間因設備維修，餐廳將提前於晚上8點停止營業。";
        }else return;
                       
        Log.Information("Sending prompt to GPT: {Prompt}", systemPrompt);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage("客戶預約資訊文本：\n" + record.TranscriptionText + "\n\n")
        };

        var completion = await client.CompleteChatAsync(messages, new ChatCompletionOptions { ResponseModalities = ChatResponseModalities.Text, ResponseFormat = ChatResponseFormat.CreateTextFormat() }, cancellationToken).ConfigureAwait(false);
        var jsonResponse = completion.Value.Content.FirstOrDefault()?.Text ?? "";
        
        Log.Information("AI JSON Response: {JsonResponse}", jsonResponse);
                    
        var information = new PhoneOrderReservationInformation
        {
            RecordId = record.Id,
            NotificationInfo = jsonResponse,
            AiNotificationInfo = jsonResponse
        };
                    
        await _phoneOrderDataProvider.AddPhoneOrderReservationInformationAsync(information, cancellationToken: cancellationToken).ConfigureAwait(false);
                    
        if (!store.IsManualReview)
        {
            var merchPrinter = (await _printerDataProvider.GetMerchPrintersAsync(storeId: store.Id, isEnabled: true, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();

            Log.Information("get merch printer:{@merchPrinter}", merchPrinter);
                        
            var order = new MerchPrinterOrder
            {
                OrderId = record.Id,
                StoreId = store.Id,
                PrinterMac = merchPrinter?.PrinterMac,
                PrintDate = DateTimeOffset.Now,
                PrintFormat = PrintFormat.Draft
            };
        
            Log.Information("Create merch printer order:{@merchPrinterOrder}", order);
                 
            await _printerDataProvider.AddMerchPrinterOrderAsync(order, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<List<PhoneOrderOrderItem>> GetSimilarRestaurantByRecordAsync(PhoneOrderRecord record, PhoneOrderDetailDto foods, CancellationToken cancellationToken)
    {
        if (record == null || foods?.FoodDetails == null || foods.FoodDetails.Count == 0) return [];
        
        var restaurant = await _restaurantDataProvider.GetRestaurantByNameAsync(record.RestaurantInfo?.Name, cancellationToken).ConfigureAwait(false);
        
        if (restaurant == null) return [];
        
        var tasks = foods.FoodDetails.Where(x => !string.IsNullOrWhiteSpace(x?.FoodName)).Select(async foodDetail =>
        {
            var similarFoodsResponse = await _vectorDb.GetSimilarListAsync(
                restaurant.Id.ToString(), foodDetail.FoodName, minRelevance: 0.4, cancellationToken: cancellationToken).ToListAsync(cancellationToken);

            if (similarFoodsResponse.Count == 0) return null;
            
            var payload = similarFoodsResponse.First().Item1.Payload[VectorDbStore.ReservedRestaurantPayload].ToString();
            
            if (string.IsNullOrEmpty(payload)) return null;
            
            foodDetail.FoodName = JsonConvert.DeserializeObject<RestaurantPayloadDto>(payload).Name;
            foodDetail.Price = (double)JsonConvert.DeserializeObject<RestaurantPayloadDto>(payload).Price;
            foodDetail.ProductId = JsonConvert.DeserializeObject<RestaurantPayloadDto>(payload).ProductId;
            
            return foodDetail;
        }).ToList();

        var result = await Task.WhenAll(tasks);

        return result.Where(fd => fd != null).Select(x => new PhoneOrderOrderItem
        {
            RecordId = record.Id,
            FoodName = x.FoodName,
            Quantity = int.TryParse(x.Count, out var parsedValue) ? parsedValue : 1,
            Price = x.Price,
            Note = x.Remark,
            ProductId = x.ProductId
        }).ToList();
    }
    
    private async Task<PosOrder> MatchSimilarProductsAsync(PhoneOrderRecord record, PhoneOrderDetailDto foods, CancellationToken cancellationToken)
    {
        if (record == null || foods?.FoodDetails == null) return null;
        
        var store = await _posDataProvider.GetPosStoreByAgentIdAsync(record.AgentId, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Generate pos order for store: {@Store} by agentId: {AgentId}", store, record.AgentId);
        
        if (store == null) return null;

        var tasks = foods.FoodDetails.Where(x => !string.IsNullOrWhiteSpace(x?.FoodName)).Select(async foodDetail =>
        {
            var similarFoodsResponse = await _vectorDb.GetSimilarListAsync(
                $"pos-{store.Id}", foodDetail.FoodName, minRelevance: 0.4, cancellationToken: cancellationToken).ToListAsync(cancellationToken);

            if (similarFoodsResponse.Count == 0) return null;
            
            var payload = similarFoodsResponse.First().Item1.Payload[VectorDbStore.ReservedPosProductPayload].ToString();
            
            if (string.IsNullOrEmpty(payload)) return null;

            var productPayload = JsonConvert.DeserializeObject<PosProductPayloadDto>(payload);
            foodDetail.FoodName = productPayload.Names;
            foodDetail.Price = (double)productPayload.Price;
            foodDetail.ProductId = productPayload.ProductId;
            
            return new SimilarResult
            {
                Id = productPayload.Id,
                FoodDetail = foodDetail,
                LanguageCode = productPayload.LanguageCode
            };
        }).ToList();

        var completedTasks = await Task.WhenAll(tasks);

        var results = completedTasks.Where(x => x != null && x.Id != 0).ToList();
        
        return await BuildPosOrderAsync(foods.Type, record, store, results, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PosOrder> BuildPosOrderAsync(int type, PhoneOrderRecord record, CompanyStore store, List<SimilarResult> similarResults, CancellationToken cancellationToken)
    {
        var products = await _posDataProvider.GetPosProductsAsync(
            storeId: store.Id, ids: similarResults.Select(x => x.Id).ToList(), isActive: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        var taxes = GetOrderItemTaxes(products, similarResults);
        
        return await _redisSafeRunner.ExecuteWithLockAsync($"generate-order-number-{store.Id}", async() =>
        {
            var items = BuildPosOrderItems(products, similarResults);
            
            var orderNo = await GenerateOrderNumberAsync(store, cancellationToken).ConfigureAwait(false);
            
            var order = new PosOrder
            {
                StoreId = store.Id,
                Name = record?.CustomerName ?? "Unknown",
                Phone = !string.IsNullOrWhiteSpace(record?.PhoneNumber) ? record.PhoneNumber : !string.IsNullOrWhiteSpace(record?.IncomingCallNumber) ? record.IncomingCallNumber.Replace("+1", "") : "Unknown",
                Address = string.Empty,
                OrderNo = orderNo,
                Status = PosOrderStatus.Pending,
                Count = items.Sum(x => x.Quantity),
                Tax = taxes,
                Total = items.Sum(p => p.Price * p.Quantity) + taxes,
                SubTotal = items.Sum(p => p.Price * p.Quantity),
                Type = (PosOrderReceiveType)type,
                Items = JsonConvert.SerializeObject(items),
                Notes = record?.Comments ?? string.Empty,
                RecordId = record!.Id
            };
            
            Log.Information("Generate complete order: {@Order}", order);
        
            await _posDataProvider.AddPosOrdersAsync([order], cancellationToken: cancellationToken).ConfigureAwait(false);
            
            return order;
        }, wait: TimeSpan.FromSeconds(10), retry: TimeSpan.FromSeconds(1), server: RedisServer.System).ConfigureAwait(false);
    }

    private async Task<string> GenerateOrderNumberAsync(CompanyStore store, CancellationToken cancellationToken)
    {
        var (utcStart,utcEnd) = GetUtcMidnightForTimeZone(DateTimeOffset.UtcNow, store.Timezone);
        
        var preOrder = await _posDataProvider.GetPosOrderSortByOrderNoAsync(store.Id, utcStart, utcEnd, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (preOrder == null) return "0001";

        var rs = Convert.ToInt32(preOrder.OrderNo);
        
        rs++;
        
        return rs.ToString("D4");
    }
    
    private string TimezoneMapping(string timezone)
    {
        if (string.IsNullOrEmpty(timezone)) return "Pacific Standard Time";
        
        return timezone.Trim() switch
        {
            "America/Los_Angeles" => "Pacific Standard Time",
            _ => timezone.Trim()
        };
    }
    
    private (DateTimeOffset utcStart, DateTimeOffset utcEnd) GetUtcMidnightForTimeZone(DateTimeOffset utcNow, string timezone)
    {
        var windowsId = TimezoneMapping(timezone);
        var tz = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        
        var localTime = TimeZoneInfo.ConvertTime(utcNow, tz);
        var localMidnight = new DateTime(localTime.Year, localTime.Month, localTime.Day, 0, 0, 0);
        var localStart = new DateTimeOffset(localMidnight, tz.GetUtcOffset(localMidnight));
        
        var utcStart = localStart.ToUniversalTime();
        var utcEnd = utcStart.AddDays(1);
        
        return (utcStart, utcEnd);
    }

    private decimal GetOrderItemTaxes(List<PosProduct> products, List<SimilarResult> similarResults)
    {
        decimal taxes = 0;
        var productMap = new Dictionary<PosProduct, int>();
        
        foreach (var product in products)
        {
            var result = similarResults.Where(x => x.Id == product.Id).FirstOrDefault();

            if (result == null ) continue;
            
            productMap.Add(product, int.TryParse(result.FoodDetail.Count, out var parsedValue) ? parsedValue : 1);
        }
        
        foreach (var (product, quantity) in productMap)
        {
            var productTaxes = JsonConvert.DeserializeObject<List<EasyPosResponseTax>>(product.Tax);
            
            var productTax = productTaxes?.FirstOrDefault()?.Value;

            taxes += productTax.HasValue ? product.Price * quantity * (productTax.Value / 100) : 0;
            
            var modifiers = !string.IsNullOrEmpty(product.Modifiers) ? JsonConvert.DeserializeObject<List<EasyPosResponseModifierGroups>>(product.Modifiers) : [];

            taxes += modifiers.Sum(modifier => modifier.ModifierProducts.Sum(x => (x?.Price ?? 0) * ((modifier.Taxes?.FirstOrDefault()?.Value ?? 0) / 100)));
        }
        
        Log.Information("Calculate order item taxes: {Taxes}", taxes);
        
        return taxes;
    }

    private List<PhoneCallOrderItem> BuildPosOrderItems(List<PosProduct> products, List<SimilarResult> similarResults)
    {
        EnrichSimilarResults(products, similarResults);
        
        var orderItems = similarResults.Where(x => x.Product != null).Select(x => new PhoneCallOrderItem
        {
            ProductId = Convert.ToInt64(x.Product.ProductId),
            Quantity = int.TryParse(x.FoodDetail.Count, out var parsedValue) ? parsedValue : 1,
            OriginalPrice = x.Product.Price,
            Price = x.Product.Price,
            Notes = string.IsNullOrWhiteSpace(x.FoodDetail?.Remark) ? string.Empty : x.FoodDetail?.Remark,
            OrderItemModifiers = HandleSpecialItems(x.Product)
        }).Where(x => x.ProductId != 0).ToList();
        
        Log.Information("Generate order items: {@orderItems}", orderItems);
            
        return orderItems;
    }

    private void EnrichSimilarResults(List<PosProduct> products, List<SimilarResult> similarResults)
    {
        foreach (var productIdInfo in similarResults.Where(productIdInfo => products.Any(x => x.Id == productIdInfo.Id)))
        {
            productIdInfo.Product = products.First(x => x.Id == productIdInfo.Id);
        }
    }

    private List<PhoneCallOrderItemModifiers> HandleSpecialItems(PosProduct product)
    {
        var result = !string.IsNullOrWhiteSpace(product?.Modifiers) ? JsonConvert.DeserializeObject<List<EasyPosResponseModifierGroups>>(product.Modifiers) : [];

        if (result == null || result.Count == 0) return [];
        
        var orderItemModifiers = new List<PhoneCallOrderItemModifiers>();
        
        foreach (var modifierItem in result)
        {
            var items = modifierItem.ModifierProducts.Select(x => new PhoneCallOrderItemModifiers
            {
                Price = x?.Price ?? 0,
                Quantity = 1,
                ModifierId = modifierItem.Id,
                ModifierProductId = x?.Id ?? 0,
                Localizations = _mapper.Map<List<PhoneCallOrderItemLocalization>>(modifierItem.Localizations ?? []),
                ModifierLocalizations = _mapper.Map<List<PhoneCallOrderItemModifierLocalization>>(x?.Localizations ?? [])
            });
            
            orderItemModifiers.AddRange(items);
        }
        
        Log.Information("Generate order item: {@Product} modifiers: {@OrderItemModifiers}", product, orderItemModifiers);
        
        return orderItemModifiers;
    }

    private async Task HandleSalesOrderAsync(CancellationToken cancellationToken)
    {
        // ToDo: Place order to hifood
    }
    
    private async Task HandlePosOrderAsync(PosOrder order, CancellationToken cancellationToken)
    {
        if (order == null) return;
        
        await _posService.HandlePosOrderAsync(order, false, cancellationToken).ConfigureAwait(false);
    }
}

public class SimilarResult
{
    public int Id { get; set; }
    
    public string LanguageCode { get; set; }
    
    public PosProduct Product { get; set; }
    
    public FoodDetailDto FoodDetail { get; set; }
}