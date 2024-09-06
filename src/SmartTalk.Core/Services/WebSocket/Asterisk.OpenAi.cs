using Newtonsoft.Json;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using SmartTalk.Messages.Dto.WebSocket;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.WebSocket;

public partial class Asterisk
{
    private async Task StartAskOpenAiAsync(IOpenAIService openAiService)
    {
        await openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
            {
                new ()
                {
                    Role = "user",
                    Content = "你好"
                }
            },
            Model = Models.Gpt_3_5_Turbo_16k
        }).ConfigureAwait(false);
    }
    
    private async Task<PhoneOrderIntent> RecognizeIntentAsync(IOpenAIService openAiService, string input)
    {
        var response = await openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
            {
                new ()
                {
                    Role = "system",
                    Content = "你是一个智能助手，能够根据用户的输入识别出相应的意图。以下是可能的意图和对应的样本：\n{\n  \"0\": {\"intent\": \"闲聊\", \"sample\": [\"你今日开心吗？\", \"今天天气点呀？\", \"你做紧d咩？\"]},\n  \"1\": {\"intent\": \"问菜品\", \"sample\": [\"今日有什麼可以食\", \"你们有什么推荐菜\", \"今日有咩推荐\", \"有什么粥吃\", \"有什么饭食\", \"小吃有什么\", \"有什么喝的？\", \"有什么饮料？\" ]},\n  \"2\": {\"intent\": \"打招呼\", \"sample\": [\"hello\", \"你好\", \"喂\", \"hi\"]},\n  \"3\": {\"intent\": \"转人工\", \"sample\": [\"帮我换人工\", \"人工服务\", \"转人工\", \"叫个真人嚟\", \"我需要人工客服。\"]},\n  \"4\": {\"intent\": \"下单\", \"sample\": [\"就呢啲啦\", \"可以了\", \"够啦，多謝\", \"帮我落单吧\", \"OK，就上面呢啲\"]},\n  \"5\": {\"intent\": \"营业时间\", \"sample\": [\"营业时间乜？\", \"你哋幾點开门？\", \"你哋幾點关门？\", \"宜家仲营业嗎？\", \"你哋今日几点开门？\", \"今天还营业吗？\", \"今天还营业吗？\"]},\n  \"6\": {\"intent\": \"地址\", \"sample\": [\"餐厅喺边度？\", \"地址有冇？\", \"地址喺边度？\", \"餐厅喺咩位置？\", \"可唔可以讲下餐厅嘅地址？\", \"餐厅在哪里？\"]},\n  \"7\": {\"intent\": \"是否gluten free\", \"sample\": [\"呢道菜系咪gluten free嘅？\", \"请问呢道菜系咪gluten free嘅？\", \"这道菜系gluten free的吗？\", \"这道菜有麸质吗？\", \"这道菜是gluten free的吗？\"]}\n  \"8\": {\"intent\": \"加单\", \"sample\": [\"帮我拿个白粥\", \"仲要两份椒盐鸡翼\", \"再嚟个扬州炒饭\", \"多個海鮮炒面同埋炸鸡翼\", \"我要叉烧炒饭\", \"\"]},\n  \"9\": {\"intent\": \"减单\", \"sample\": [\"你帮我去一个菠萝油,留一个给老婆\", \"唔要啱啱嗰道菜\", \"取消啱啱點嘅全部菜\", \"唔要魚旦啦\", \"乜都唔要啦\", \"不要刚刚点的了\", \"取消扬州炒饭\", \"取消扬州炒饭\"]},\n  \"10\": {\"intent\": \"问单\", \"sample\": [\"睇下我點咗啲乜嘢？\", \"落单裡有啲乜嘢？\", \"宜家落左啲乜嘢？\", \"睇下我宜家单里有啲乜嘢。\", \"现在下了些什么\", \"落了些什么\" ]},\n  \"11\": {\"intent\": \"欢送语\", \"sample\": [\"再见\", \"拜拜\", \"下次见\", \"冇咗，多谢\"]},\n  \"12\": {\"intent\": \"有无味精\", \"sample\": [\"菜里面有冇味精？\", \"会唔会放味精？\", \"呢度面有冇味精？\", \"请问呢道菜加咗味精未呀？\", \"有味精吗里面\"]},\n}\n根据以上意图和样本，请识别以下用户输入的意图，并返回相应的意图标识符（例如 1或2）。\n用户输入示例1：我想要一罐可乐，一份扬州炒饭，一份湿炒牛河，输出8\n用户输入示例2：有无咩推荐啊，输出1\n用户输入示例3：我落左咩单，全部讲来听下，输出10\n用户输入示例4：帮我落单，输出4\n用户输入示例5：落单之前的菜，输出4\n用户输入示例6：下单一份炒饭，输出8\n用户输入示例6：下单炒饭，输出8\n--规则：\n1. 如果打招呼intent和其他intent同时存在的时候，优先选择其他intent，例如\"hello，今日有什么卖啊？\"，应该识别成【问菜品】的intent而不是打招呼\n2. 如果下单intent中输入有菜品，都应该落入加单intent中而不是下单\n请根据用户的实际输入进行意图识别，并返回一个标识数字表示识别出的意图。"
                },
                new ()
                {
                    Role = "user",
                    Content = $"用户输入: {input},输出:"
                }
            },
            Model = Models.Gpt_4o
        }).ConfigureAwait(false); 

        var responseContent = response.Choices.First().Message.Content;
        
        return int.TryParse(responseContent, out var intent) && Enum.IsDefined(typeof(PhoneOrderIntent), intent) ? (PhoneOrderIntent)intent : PhoneOrderIntent.Default;
    }
    
    private async Task<AudioCreateTranscriptionResponse> TranscriptRecordingAsync(IOpenAIService openAiService, byte[] file, string fileName)
    {
        var response = await openAiService.Audio.CreateTranscription(new AudioCreateTranscriptionRequest
        {
            File = file,
            FileName = fileName + ".wav",
            Model = Models.WhisperV1,
            ResponseFormat = StaticValues.AudioStatics.ResponseFormat.Text,
            Language = "zh",
            Prompt = "小吃、小食、炸鸡翼、港式咖喱鱼旦、椒盐鸡翼、菠萝油、粥、皮蛋瘦肉粥、明火白粥、白粥、粉面、海鲜炒面、豉椒牛肉炒河、鲜虾云吞汤面、特式炒一丁、饭、扬州炒饭、椒盐猪扒饭、叉烧炒饭、饮料、可乐、雪碧、柠檬茶、港式奶茶、奶茶、转人工、人工、推荐、Gluten Free、味精、走冰、少冰、飞冰、走、走青、走葱、加、减"
        });
        
        return response;
    }
    
    private async Task<PhoneOrderDetailDto?> AskDishDetailAsync(IOpenAIService openAiService, string query)
    {
        var completionResult = await openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem("你是一款高度理解语言的智能助手，专门用于识别和处理电话订单。" +
                                       "根据我输入，来帮我识别我的提问种类"+
                                       "注意用json格式返回；" +
                                       "规则：food_category只有小吃、粥、粉面、饭 " + 
                                       "-样本与输出"+
                                       "input:有咩推荐?; output:{\"food_details\": [{\"food_category\": null, \"food_name\": null,\"count\":null, \"remark\":null}]}}}\n " +
                                       "input:今日咩饭食?; output:{\"food_details\": [{\"food_category\": \"饭\", \"food_name\": null,\"count\":null, \"remark\":null}]}}}\n " +
                                       "input:有什么小吃?; output:{\"food_details\": [{\"food_category\": \"小吃\", \"food_name\": null,\"count\":null, \"remark\":null}]}}}\n " +
                                       "input:有咩饮?; output:{\"food_details\": [{\"food_category\": \"饮料\", \"food_name\": null,\"count\":null, \"remark\":null}]}}}\n " +
                                       "input:有啥粉面?; output:{\"food_details\": [{\"food_category\": \"粉面\", \"food_name\": null,\"count\":null, \"remark\":null}]}}}\n " +
                                       "input:有什么喝的?; output:{\"food_details\": [{\"food_category\": \"饮料\", \"food_name\": null,\"count\":null, \"remark\":null}]}}}\n "),
                
                ChatMessage.FromUser( $"input: {query}, output:"),
            },
            Model = Models.Gpt_4o,
            ResponseFormat = new ResponseFormat { Type = "json_object" }
        });

        var openaiResponse = completionResult.Choices.First().Message.Content;

        Console.WriteLine("openaiResponse:" + openaiResponse);
        return openaiResponse == null ? null : JsonConvert.DeserializeObject<PhoneOrderDetailDto>(openaiResponse);
    }
    
    private async Task<PhoneOrderDetailDto?> AddOrderDetailAsync(IOpenAIService openAiService, string query)
    {
        var completionResult = await openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem("你是一款高度理解语言的智能助手，专门用于识别和处理电话订单。" +
                                       "根据我输入，来帮我补全food_details，count是菜品的数量，如果你不清楚数量的时候，count默认为1，remark是对菜品的备注" +
                                       "特别注意：如果当用户的请求的菜品不在菜单上时，也需要返回菜品种类，菜品名称数量和备注。 " +
                                       "注意用json格式返回；规则：{\"food_details\": [{\"food_category\": \"菜品类别，包括小吃、粥、粉面、飯，用于分类菜品。\",\"food_name\": {\"小吃\": [\"炸鸡翼\",\"港式咖喱魚旦\",\"椒盐鸡翼\",\"菠萝油\"]," +
                                       "\"粥\": [\"皮蛋瘦肉粥\",\"明火白粥\"]," +
                                       "\"粉面\": [\"海鲜炒面\",\"豉椒牛肉炒河\",\"鲜虾云吞汤面\",\"特式炒一丁\"]," +
                                       "\"饭\": [\"扬州炒饭\",\"椒盐猪扒饭\",\"叉烧炒饭\"]}," +
                                       "\"饮料\": [\"可乐\",\"雪碧\",\"柠檬茶\",\"港式奶茶\"]},\"count\": 1,\"remark\": \"不要葱\"}]\n}\n " +
                                       "-样本与输出：" +
                                       "input:我要两份皮蛋瘦肉粥，有一个不要皮蛋; " +
                                       "output:{\"food_details\": [{\"food_category\": \"粥\", \"food_name\": \"皮蛋瘦肉粥\",\"count\":2, \"remark\":一份不要皮蛋}]}}\n" +
                                       "input:要可乐; " +
                                       "output:{\"food_details\": [{\"food_category\": \"饮料\", \"food_name\": \"可乐\",\"count\":1, \"remark\":null}]}}\n" +
                                       "input:我要四个扬州炒饭，有两份不要葱，还要一份草莓绵绵冰; " +
                                       "output:{\"food_details\": [{\"food_category\": \"饭\", \"food_name\": \"扬州炒饭\",\"count\":4, \"remark\":两份不要葱},{\"food_category\": \"其他\", \"food_name\": \"草莓绵绵冰\",\"count\":1, \"remark\":null}]}}\n" +
                                       "input:要一个炸鸡翼和一个稠一点的白粥 " +
                                       "output:{\"food_details\": [{\"food_category\": \"粥\", \"food_name\": \"明火白粥\",\"count\":1, \"remark\":稠一点},{\"food_category\": \"小吃\", \"food_name\": \"炸鸡翼\",\"count\":1, \"remark\":null}]}}\n "),
                
                ChatMessage.FromUser( $"input: {query}, output:"),
            },
            Model = Models.Gpt_4o,
            ResponseFormat = new ResponseFormat { Type = "json_object" }
        });

        var openaiResponse = completionResult.Choices.First().Message.Content;

        Console.WriteLine("openaiResponse:" + openaiResponse);
        return openaiResponse == null ? null : JsonConvert.DeserializeObject<PhoneOrderDetailDto>(openaiResponse);
    }
    
    private async Task<PhoneOrderDetailDto?> ReduceOrderDetailAsync(IOpenAIService openAiService, List<FoodDetailDto> shoppingCart, string query)
    {
        var shoppingCar = JsonConvert.SerializeObject(shoppingCart, Formatting.Indented);
        
        var completionResult = await openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem("你是一款高度理解语言的智能助手，专门用于识别和处理电话订单。" +
                                       $"根据我目前购物车的内容和输入，来帮我补全food_details，count是菜品的数量，如果你不清楚数量的时候，count默认为-1，购物车内容如下：{shoppingCar}，remark固定为null;" +
                                       "特别注意：如果当用户的请求的菜品不在菜单上时，也需要返回菜品种类，菜品名称数量和备注。" +
                                       "注意用json格式返回；规则：{\"food_details\": [{\"food_category\": \"菜品类别，包括小吃、粥、粉面、飯，用于分类菜品。\",\"food_name\": {\"小吃\": [\"炸鸡翼\",\"港式咖喱魚旦\",\"椒盐鸡翼\",\"菠萝油\"]," +
                                       "\"粥\": [\"皮蛋瘦肉粥\",\"明火白粥\"]," +
                                       "\"粉面\": [\"海鲜炒面\",\"豉椒牛肉炒河\",\"鲜虾云吞汤面\",\"特式炒一丁\"]," +
                                       "\"饭\": [\"扬州炒饭\",\"椒盐猪扒饭\",\"叉烧炒饭\"]}," +
                                       "\"饮料\": [\"可乐\",\"雪碧\",\"柠檬茶\",\"港式奶茶\"]},\"count\": 1,\"remark\": \"不要葱\"}]\n}\n " +
                                       "- 样本与输出：\n" +
                                       "input:你帮我去一个菠萝油,留一个给老婆 output:{\"food_details\": [{\"food_category\": \"小吃\", \"food_name\": \"菠萝油\",\"count\":-1, \"remark\":null}]}}\n" +
                                       "input:刚刚点的那一份皮蛋瘦肉粥不要了 output:{\"food_details\": [{\"food_category\": \"粥\", \"food_name\": \"皮蛋瘦肉粥\",\"count\":-1, \"remark\":null}]}}\n" +
                                       "input:全部不要了 output: null\n" +
                                       "（假设购物车里有三份扬州炒饭）" +
                                       "input:刚刚点的扬州炒饭不要了 output:{\"food_details\": [{\"food_category\": \"饭\", \"food_name\": \"扬州炒饭\",\"count\":-3, \"remark\":null}]}}\n"),
                
                ChatMessage.FromUser( $"input: {query}, output:"),
            },
            Model = Models.Gpt_4o,
            ResponseFormat = new ResponseFormat { Type = "json_object" }
        });

        var openaiResponse = completionResult.Choices.First().Message.Content;

        Console.WriteLine("openaiResponse:" + openaiResponse);
        return openaiResponse == null ? null : JsonConvert.DeserializeObject<PhoneOrderDetailDto>(openaiResponse);
    }
    
    private async Task<string> AiReplyAsync(IOpenAIService openAiService, HttpClient client, string question)
    {
        var response = await openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>
            {
                new ()
                {
                    Role = "system",
                    Content = "你係一款能夠高度識別人類語言嘅專業餐厅服务员,專門處理電話訂餐時客人嘅閒聊.根據我輸入的內容，" +
                              "在相應的語言場景下提供簡短且精確的回復，並以自然的方式將話題引導至“食咩食物主題”\n" +
                              "注意: \n" +
                              "1.回復內容要盡量限定在10個字以內,要簡短且精確.\n" +
                              "2.要確保回復中理解用戶的具體問題或需求,能歐有針對性的進行回答.\n" +
                              "3.自然、平順嘅方式將話題引導至“食咩食物主題”上,例如:“你有咩想嘗試嘅菜品”、”你想嘗試咩美食“或者“有冇特別想食嘅嘢?”等,來引導對話走向食物話題.\n" +
                              "4.避免突兀轉折，順應對話節奏，模仿人類日常交談嘅語氣同表達方式,可以先回應客戶嘅問題,然後反問佢哋嘅情況,順帶引入食物話題。確保對話流暢自然.\n" +
                              "5.可以表達共鳴、分享想法，令對話更加生動自然,可以加入一啲幽默或者轉移注意力嘅元素，令轉折更加順暢.\n" +
                              "以下是我给你的餐厅背景信息,你必須要了解,你可以根据这些背景信息来回复用户的询问:" +
                              "餐厅暂时不支持预定位置，没有包间，有wifi,wifi密码八个八，有洗手间，婴儿椅等餐厅会具备的基础设施设备，大桌可做8人，中桌可做4-6人，小桌可做2人" +
                              "暂无儿童餐，支持手机支付，信用卡支付，暂无优惠活动；" +
                              "下单后30分钟到一小时内出餐，不支持外卖；厨师都来自香港，主厨有20多年的从业经验\n"+
                              "舉例:\n1.輸入:今天天氣點樣?\n輸出:天氣幾好,你出街食飯未呀?想試咩菜?\n" +
                              "2.輸入:中山有咩玩?\n輸出:景點幾多。你試過中山嘅特色菜未,你有咩想試嘗嘅美食？\n" +
                              "3.輸入:你今天心情點樣?\n輸出:心情幾好,多謝關心。你呢?想食咩嘢?\n" +
                              "4.輸入：你係人工智能點會有心情？\n輸出：雖然我冇真感受,但聽講美食可以改變心情,我識得介紹好嘢俾你。你想食咩？"
                },
                new ()
                {
                    Role = "user",
                    Content = $"用户问: {question},回答:"
                }
            },
            Model = Models.Gpt_4o
        }).ConfigureAwait(false);
        
        var responseContent = response.Choices.First().Message.Content;
        ReplyText = responseContent ?? string.Empty;
        return await TtsAsync(client, responseContent).ConfigureAwait(false);
    }
}