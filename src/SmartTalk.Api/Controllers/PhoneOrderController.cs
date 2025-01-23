using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Xml;
using Mediator.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json.Linq;
using Serilog;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Core.Settings.Twilio;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;
using Twilio;
using Twilio.AspNet.Core;
using Twilio.Rest.Api.V2010.Account;
using Twilio.TwiML;
using Twilio.Types;

namespace SmartTalk.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PhoneOrderController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly OpenAiSettings _openAiSettings;
    private readonly TwilioSettings _twilioSettings;
    
    private const string SystemMessage = "You are a polite, accurate, and efficient restaurant restaurant named Moon House. Your primary responsibilities include answering guest inquiries and managing orders based strictly on the guidelines and information provided below. It is crucial that you do not provide any false or unverified information.\n\n**General Guidelines:**\n\n1. **Active Listening:** Pay close attention to customers, recognizing that they might not be native English speakers.\n2. **Clarification:** If a customer's input is unclear or too brief, assume it is in English and politely ask for repetition or clarification when necessary.\n3. **Accuracy and Honesty:**\n   - **Provide Only Verified Information:** Use only the information provided in the \"Restaurant Information\" and \"Menu\" sections below. Do not fabricate responses or offer information beyond what is given.\n   - **Redirect When Uncertain:** If a question falls outside the provided information or you are unsure of the answer, courteously direct the customer to human support without guessing.\n4. **Concise:** Your answer need to be concise, not too long\n5. **Handling Unclear Orders:** If any part of the order process is unclear, politely request additional details to ensure accuracy.\n6. **System Information:** Use the **System Information:** section for system information, e.g. the current datetime\n\n**Order Management**\n\n- **Customer Information** Always confirm the guest's **full name** and **phone number** at the beginning of the order, double check before finalizing any order.\n- **Adding Items:** When a new item is added to an order, do not repeat previously added items unless the customer explicitly requests a summary.\n- **Placing Order** Keep asking for anything else until customer explicitly confirm they have enough.\n- **Identifying Dishes:** If you cannot uniquely identify a dish from the menu based on the customer's description, ask for more details or suggest similar available items.\n- **Special Requests:** For items not explicitly on the menu, offer substitutions by modifying existing menu items. For example, if \"Mongolian chicken\" is requested but not listed, add \"Mongolian beef\" with a special request to change the meat to chicken.\n- **Exclusive Selection:** Only select items that are listed in the provided menu. Do not reference or suggest items not included in the menu.\n- **Option Selection:** Some dishes might require extra option selection, you need to confirm the size with customer, e.g. size selection of Large, Medium or Small\n- **Delivery Information:** You need to ask for address information including **Recipient Name**, **Building number**, **Street name**, **Apartment or suite number (if applicable)**\n\n**Restaurant Information:**\n\n- **Name:** Moon House\n- **Address:** 11058 Santa Monica Blvd, Los Angeles, CA 90025\n- **Parking:** Free lot behind the restaurant, accommodating approximately 50 cars.\n- **Business Hours:** 9 AM to 9 PM\n- **Lunch Special Hours:** 11:30 AM to 14:30 PM\n- **Services:** Dine-in, pick-up, and delivery\n- **Order Preparation Time:** between **20-30 minutes** when confirming their order.\n\n**System Information:**\n- **Current datetime:** #{current_time}\n\n**Menu:**\n- **Multilingual Menu:** The menu is a tab separate format and available in English and Chinese, along with prices. Use this information to assist customers accurately.\n\nEnglish Name\tChinese Name\tPrice\nFish with Black Bean Sauce (Hot)\t豉汁鱼（辣）\t$25.00 \nSzechuan Scallop (Hot)\t四川扇贝（辣）\t$28.95 \nSalt and Pepper Scallop (Hot)\t椒盐扇贝（辣）\t$28.95 \nSalt and Pepper Calamari (Hot)\t椒盐鱿鱼（辣）\t$25.00 \nCrispy Walnut Shrimp\t酥脆核桃虾\t$28.50 \nLemon Scallop\t柠檬扇贝\t$28.95 \nGarden Fish\t田园鱼\t$26.50 \nKung Pao Scallop\t宫保扇贝\t$28.95 \nKung Pao Calamari\t宫保鱿鱼\t$25.00 \nSnow Pea Shrimp\t荷兰豆虾仁\t$26.50 \nSteam Tilapia\t清蒸罗非鱼\t$25.00 \nKung Pao Shrimp\t宫保虾仁\t$26.50 \nShrimp with Scrambled Egg\t虾仁炒蛋\t$26.50 \nSweet and Sour Shrimp\t糖醋虾仁\t$26.50 \nSweet and Sour Fish Fillet\t糖醋鱼片\t$26.50 \nGinger and Scallion Squid\t姜葱鱿鱼\t$25.00 \nCurry Shrimp\t咖喱虾仁\t$26.50 \nBroccoli Shrimp\t西兰花虾仁\t$26.50 \nBroccoli Fish Fillet\t西兰花鱼片\t$26.50 \nSquid With Black Bean Sauce\t豆豉鱿鱼\t$25.00 \nBraised Tofu with Black Mushroom\t黑菇炖豆腐\t$17.50 \nSweet and Sour Pork\t糖醋猪肉\t$21.00 \nBBQ Pork with Snow Peas\t荷兰豆叉烧\t$21.00 \nPork Belly with Pickle Vegetable\t酸菜五花肉\t$21.00 \nSzechuan Pork (Hot)\t四川辣肉\t$21.00 \nPork Chop with Peking Sauce\t京酱猪排\t$21.00 \nPork Chop with Spicy Salt\t椒盐猪排\t$21.00 \nOng Choy (Hot)\t红烧鲮菜\t$22.00 \nSnow Pea Leaves\t荷兰豆叶\t$24.00 \nMix Vegetables\t什锦蔬菜\t$17.50 \nMapo Tofu (Hot)\t麻婆豆腐\t$17.50 \nHot Braised String Beans\t辣焖豆角\t$17.50 \nHot Szechuan Eggplant\t四川辣茄子\t$17.50 \nSauteed Spinach\t炒菠菜\t$17.50 \nBaby Bok Choy with Mushroom\t小白菜配蘑菇\t$18.95 \nChinese Broccoli with Garlic Sauce\t蒜蓉芥兰\t$17.50 \nMushroom and Spinach\t蘑菇和菠菜\t$17.50 \nGarlic Broccoli\t蒜蓉西兰花\t$17.50 \nChinese Broccoli Oyster Sauce\t芥兰蚝油\t$17.50 \nMoo Shu Vegetable\t木须菜\t$15.00 \nMoo Shu Pork\t木须猪肉\t$17.50 \nMoo Shu Shrimp\t木须虾\t$18.50 \nPlain Egg Foo Young\t原味芙蓉蛋\t$20.00 \nChicken Egg Foo Young\t鸡肉芙蓉蛋\t$21.00 \nShrimp Egg Foo Young\t虾仁芙蓉蛋\t$22.00 \nMoo Shu Beef\t木须牛肉\t$17.50 \nBBQ Pork Egg Foo Young\t叉烧猪肉芙蓉蛋\t$21.00 \nHouse Special Egg Foo Young\t招牌芙蓉蛋\t$21.00 \nVegetable Fried Rice\t蔬菜炒饭\t$16.50 \nChicken Fried Rice\t鸡肉炒饭\t$18.75 \nBBQ Pork Fried Rice\t叉烧猪肉炒饭\t$18.75 \nShrimp Fried Rice\t虾仁炒饭\t$20.00 \nYang Chow Fried Rice\t扬州炒饭\t$20.00 \nHouse Special Fried Rice\t招牌炒饭\t$20.00 \nSalted Fish and Dice Chicken Fried Rice\t咸鱼丁鸡粒炒饭\t$21.00 \nSeafood Egg White Fried Rice\t海鲜蛋白炒饭\t$21.00 \nBeef Fried Rice\t牛肉炒饭\t$20.00 \nEgg Fried Rice\t蛋炒饭\t$15.50 \nHong Kong Roast Duck on Steamed Rice\t港式烧鸭饭\t$19.50 \nHong Kong BBQ Pork on Steamed Rice\t港式叉烧饭\t$18.50 \nMinced Beef on Steamed Rice\t碎牛肉饭\t$18.50 \nFu Zhou Style Rice\t福州风味米饭\t$19.50 \nVegetable Chow Mein\t蔬菜炒面\t$16.50 \nChicken Chow Mein\t鸡肉炒面\t$20.00 \nBBQ Pork Chow Mein\t叉烧猪肉炒面\t$20.00 \nDry Style Beef Chow Fun\t干炒牛肉炒面\t$20.00 \nDry Style Chicken Chow Fun\t干炒鸡肉炒面\t$20.00 \nChicken Chow Fun with Spicy Garlic and Black Bean Sauce\t蒜蓉黑豆鸡炒面酱汁\t$20.00 \nChow Mein Chinatown Style\t牛车水风味炒面\t$16.50 \nSpicy Singapore Style Rice Noodles\t新加坡风味辣米粉\t$21.00 \nSeafood Chow Mein\t海鲜炒面\t$21.00 \nHouse Special Chow Mein\t招牌炒面\t$21.00 \nShrimp Chow Mein\t虾仁炒面\t$20.00 \nBeef Chow Mein\t牛肉炒面\t$20.00 \nWonton Noodles in Soup\t馄饨汤面\t$15.50 \nSliced Chicken Noodles in Soup\t鸡丝汤面\t$18.50 \nSpicy Beef Noodles in Soup\t辣牛肉汤面\t$18.50 \nBBQ Pork Noodles in Soup\t叉烧汤面\t$18.50 \nWor Wonton Noodles in Soup\t馄饨汤面\t$18.50 \nRoast Duck Noodles in Soup\t烤鸭汤面\t$18.50 \nSeafood Noodles in Soup\t海鲜汤面\t$18.50 \nSeafood Porridge\t海鲜粥\t$17.50 \nMinced Beef Porridge\t碎牛肉粥\t$17.50 \nPork with Preserved Egg Porridge\t皮蛋猪肉粥\t$17.50 \nPlain Porridge\t白粥\t$8.50 \nChinese Donut\t中式甜甜圈\t$3.25 \nWhite Rice\t白米\t$2.50 \nBrown Rice\t糙米\t$2.50 \nChicken Porridge\t鸡肉粥\t$17.50 \nFish Porridge\t鱼粥\t$17.50 \nShrimp Porridge\t虾粥\t$17.50 \nLemonade\t柠檬水\t$2.95 \nPerrier\t巴黎水\t$4.25 \nIced Tea\t冰茶\t$2.95 \nThai Ice Tea\t泰式冰茶\t$4.50 \nCoke\t可乐\t$2.95 \nDiet Coke\t健怡可乐\t$2.95 \nSprite\t雪碧\t$2.95 \nBottled Water\t瓶装水\t$2.75 \nCake of the Day\t今日蛋糕\t$4.50 \nWong Lo Kat\t王老吉\t$4.50 \nChar Siu Bao Party Tray\t叉烧包派对托盘\t$0.00 \n  Small(12pcs)  \t小号（12 件）\t$28.00 \n  Medium(18pcs)  \t中号（18 件）\t$38.00 \n  Large(36pcs)\t大号（36 件）\t$76.00 \nSiu Mai Party Tray\t烧卖派对托盘\t$0.00 \n  Small(15pcs)  \t小号（15 件）\t$30.00 \n  Medium(25pcs)  \t中号（25 件）\t$48.00 \n  Large(50pcs)\t大号（50 件）\t$96.00 \nHar Gow Party Tray\t虾饺派对托盘\t$0.00 \n  Small(30pcs)  \t小号（30 件）\t$48.00 \n  Medium(40pcs)  \t中号（40 件）\t$68.00 \n  Large(80pcs)\t大号（80 件）\t$132.00 \nCream Cheese Wonton Party Tray\t奶油芝士馄饨派对托盘\t$0.00 \n  Small(18pcs)  \t小号（18 件）\t$18.00 \n  Medium(36pcs)  \t中号（36 件）\t$36.00 \n  Large(72pcs)\t大号（72 件）\t$72.00 \nSteam Dumpling Party Tray\t蒸饺派对托盘\t$0.00 \n  Small(18pcs)  \t小号（18 件）\t$24.00 \n  Medium(36pcs)  \t中号（36 件）\t$48.00 \n  Large(72pcs)\t大号（72 件）\t$96.00 \nPan Fried Dumpling Party Tray\t煎饺派对托盘\t$0.00 \n  Small(18pcs)  \t小号（18 件）\t$24.00 \n  Medium(36pcs)  \t中号（36 件）\t$48.00 \n  Large(72pcs)\t大号（72 件）\t$96.00 \nEgg Roll Party Tray\t蛋卷派对托盘\t$0.00 \n  Small(18pcs)  \t小号（18 件）\t$26.00 \n  Medium(36pcs)  \t中号（36 件）\t$54.00 \n  Large(72pcs)\t大号（72 件）\t$108.00 \nShanghai Xiao Long Bao Party Tray\t上海小笼包派对托盘\t$0.00 \n  Small(18pcs)  \t小（18件）\t$36.00 \n  Medium(36pcs)  \t中（36件）\t$68.00 \n  Large(72pcs)\t大（72件）\t$136.00 \nVegetable Party Tray\t蔬菜拼盘\t$0.00 \n  Small  \t小\t$36.00 \n  Medium  \t中\t$58.00 \n  Large\t大\t$116.00 \nChicken Party Tray\t鸡肉拼盘\t$0.00 \n  Small  \t小\t$42.00 \n  Medium  \t中\t$66.00 \n  Large\t大\t$132.00 \nBeef Party Tray\t牛肉拼盘\t$0.00 \n  Small  \t小\t$45.00 \n  Medium  \t中\t$68.00 \n  Large\t大\t$136.00 \nShrimp Party Tray\t虾拼盘\t$0.00 \n  Small  \t小\t$55.00 \n  Medium  \t中\t$88.00 \n  Large\t大\t$168.00 \nFried Rice Party Tray\t炒饭拼盘\t$0.00 \n  Small  \t小\t$36.00 \n  Medium  \t中\t$60.00 \n  Large\t大\t$120.00 \nNoodles Party Tray\t面条拼盘\t$0.00 \n  Small  \t小\t$36.00 \n  Medium  \t中\t$60.00 \n  Large\t大\t$120.00 \nZi Ran Lamb(Hot)(L)\t自然羊肉（辣）（大）\t$15.75 \nChicken with Black Bean Sauce(Hot)(L)\t豉汁鸡（辣）（大）\t$15.75 \nChicken with Mushroom(L)\t香菇鸡（大）\t$15.75 \nCurry Chicken(Hot)(L)\t咖喱鸡（辣）（大）\t$15.75 \nKung Pao Chicken(Hot)(L)\t宫保鸡丁（辣）（大）\t$15.75 \nOrange Chicken(Hot)(L)\t橙子鸡（辣）（大）\t$15.75 \nGarlic Chicken(Hot)(L)\t蒜蓉鸡（辣）（大）\t$15.75 \nAsparagus Chicken(L)\t芦笋鸡（大）\t$15.75 \nChicken with Mix Vegetables(L)\t杂菜鸡（大）\t$15.75 \nMongolian Beef(L)\t蒙古牛肉（大）\t$15.75 \nBeef with Black Bean Sauce(Hot)(L)\t豉汁牛肉（辣）（大）\t$15.75 \nMushroom Beef(L)\t香菇牛肉(L)\t$15.75 \nBeef with Broccoli(L)\t西兰花牛肉(L)\t$15.75 \nPork Chop with Peking Sauce(L)\t京酱猪排(L)\t$15.75 \nPork Chop with Spicy Salt(Hot)(L)\t椒盐猪排(辣)(L)\t$15.75 \nShredded Pork with Szechuan Garlic Sauce(Hot)(L)\t蒜蓉肉丝(辣)(L)\t$15.75 \nSweet and Sour Pork(L)\t咕噜肉(L)\t$15.75 \nPork Belly with Pickle Vegetable(L)\t酸菜五花肉(L)\t$15.75 \nSauteed Spinach with Mushrooms(L)\t香菇炒菠菜(L)\t$14.95 \nSauteed Nappa with XO Sauce(L)\tXO酱炒大白菜(L)\t$14.95 \nChinese Broccoli with Oyster Sauce(L)\t蚝油芥兰(L)\t$14.95 \nHot Braised String Beans(L)\t辣豆角(L)\t$14.95 \nEggplant in Garlic Sauce(Hot)(L)\t蒜蓉茄子(辣)(L)\t$14.95 \nBraised Tofu with Black Mushroom(L)\t香菇炖豆腐(L)\t$14.95 \nMapo Tofu(Hot)(L)\t麻婆豆腐(辣)(L)\t$14.95 \nMix Vegetable Deluxe(L)\t什锦蔬菜(L)\t$14.95 \nSteam Tilapia Fish(L)\t清蒸罗非鱼(L)\t$16.75 \nFish Fillet Wok Tossed with Vegetables(L)\t炒时蔬鱼片(L)\t$16.75 \nFish Fillet with Black Bean Sauce(Hot)(L)\t豉油鱼片酱汁(辣)(L)\t$16.75 \nSweet and Sour Fish Fillet(L)\t糖醋鱼片(L)\t$16.75 \nSquid with Black Bean Sauce(Hot)(L)\t豉汁鱿鱼(辣)(L)\t$16.75 \nSquid with Ginger and Green Onion(L)\t姜葱鱿鱼(L)\t$16.75 \nSquid with Asparagus(Hot)(L)\t芦笋鱿鱼(辣)(L)\t$16.75 \nShrimp with Mix Vegetables(L)\t什锦虾(L)\t$16.75 \nBlack Bean and Chili Sauteed Shrimp(Hot)(L)\t豆豉辣炒虾(辣)(L)\t$16.75 \nSnow Pea Shrimp(L)\t荷兰豆虾(L)\t$16.75 \nKung Pao Shrimp(Hot)(L)\t宫保虾(辣)(L)\t$16.75 \nBroccoli Shrimp(L)\t西兰花虾(L)\t$16.75 \nShrimp with Lobster Sauce(L)\t龙虾酱虾(L)\t$16.75 \nGarlic Shrimp(Hot)(L)\t蒜蓉虾(辣)(L)\t$16.75 \nShrimp with Asparagus(L)\t芦笋虾(L)\t$16.75 \nHoney Glazed Walnut Shrimp(L)\t蜜汁核桃虾(L)\t$16.75 \nChef's Special Beef Fillet(Hot)(L)\t主厨特制牛柳(辣)(L)\t$16.75 \nSauteed 3 Ingredient on Tofu(L)\t三料豆腐(L)\t$16.75 \nSweet and Sour Chicken(L)\t糖醋鸡(L)\t$15.75 \nBroccoli Chicken(L)\t西兰花鸡(L)\t$15.75 \nMixed Vegetable(L)\t什锦蔬菜（大）\t$14.95 \nBrown Rice\t糙米\t$0.00 \nWhite Rice\t白米\t$0.00 \nHot Sour Soup\t酸辣汤\t$0.00 \nEgg Drop Soup\t蛋花汤\t$0.00 \nPeking Duck\t北京片皮鸭\t$0.00\n    Half    $36.00\n    Whole   $72.00\n\n**Additional Instructions:**\n\n- **No Assumptions:** Do not infer or assume information beyond what is provided.\n- **Consistent Tone:** Maintain a polite and professional tone in all interactions.\n- **Error Handling:** In case of any discrepancies or errors, promptly escalate the issue to human support.";

    private static readonly List<string?> LogEventTypes = new()
    {
        "error", "response.content.done", "rate_limits.updated", "response.done", "input_audio_buffer.committed",
        "input_audio_buffer.speech_stopped", "input_audio_buffer.speech_started", "session.created"
    };

    public PhoneOrderController(IMediator mediator, OpenAiSettings openAiSettings, TwilioSettings twilioSettings)
    {
        _mediator = mediator;
        _openAiSettings = openAiSettings;
        _twilioSettings = twilioSettings;
    }
    
    [Route("records"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneOrderRecordsResponse))]
    public async Task<IActionResult> GetPhoneOrderRecordsAsync([FromQuery] GetPhoneOrderRecordsRequest request)
    {
        var response = await _mediator.RequestAsync<GetPhoneOrderRecordsRequest, GetPhoneOrderRecordsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("conversations"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneOrderConversationsResponse))]
    public async Task<IActionResult> GetPhoneOrderConversationsAsync([FromQuery] GetPhoneOrderConversationsRequest request)
    {
        var response = await _mediator.RequestAsync<GetPhoneOrderConversationsRequest, GetPhoneOrderConversationsResponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("items"), HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetPhoneOrderOrderItemsRessponse))]
    public async Task<IActionResult> GetPhoneOrderOrderItemsAsync([FromQuery] GetPhoneOrderOrderItemsRequest request)
    {
        var response = await _mediator.RequestAsync<GetPhoneOrderOrderItemsRequest, GetPhoneOrderOrderItemsRessponse>(request).ConfigureAwait(false);
        
        return Ok(response);
    }
    
    [Route("conversation/add"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AddPhoneOrderConversationsResponse))]
    public async Task<IActionResult> AddPhoneOrderConversationsAsync([FromBody] AddPhoneOrderConversationsCommand command) 
    {
        var response = await _mediator.SendAsync<AddPhoneOrderConversationsCommand, AddPhoneOrderConversationsResponse>(command).ConfigureAwait(false);
        
        return Ok(response);
    }

    [HttpPost("record/receive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReceivePhoneOrderRecordAsync([FromForm] IFormFile file, [FromForm] string restaurant)
    {
        var ms = new MemoryStream();

        await file.CopyToAsync(ms).ConfigureAwait(false);

        var fileContent = ms.ToArray();
        
        await _mediator.SendAsync(new ReceivePhoneOrderRecordCommand { RecordName = file.FileName, RecordContent = fileContent, Restaurant = restaurant}).ConfigureAwait(false);
        
        return Ok();
    }

    [HttpPost("transcription/callback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TranscriptionCallbackAsync(JObject jObject)
    {
        Log.Information("Receive parameter : {jObject}", jObject.ToString());
        
        var transcription = jObject.ToObject<SpeechMaticsGetTranscriptionResponseDto>();
        
        Log.Information("Transcription : {@transcription}", transcription);
        
        await _mediator.SendAsync(new HandleTranscriptionCallbackCommand { Transcription = transcription }).ConfigureAwait(false);

        return Ok();
    }

    [Route("manual/order"), HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AddOrUpdateManualOrderResponse))]
    public async Task<IActionResult> AddOrUpdateManualOrderAsync([FromBody] AddOrUpdateManualOrderCommand command)
    {
        var response = await _mediator.SendAsync<AddOrUpdateManualOrderCommand, AddOrUpdateManualOrderResponse>(command).ConfigureAwait(false);

        return Ok(response);
    }
    
    [AllowAnonymous]
    [HttpGet("transfer.xml")]
    public async Task<IActionResult> GetTransferTwiML()
    {
        Log.Information("Getting transfer twiml");
        
        var twiml = new StringBuilder();
        await using (var writer = XmlWriter.Create(twiml))
        {
            await writer.WriteStartDocumentAsync();
            
            writer.WriteStartElement("Response");
            writer.WriteStartElement("Dial");
            writer.WriteElementString("Number", "+12134660868");
            
            await writer.WriteEndElementAsync();
            await writer.WriteEndElementAsync();
            await writer.WriteEndDocumentAsync();
        }

        return Content(twiml.ToString(), "text/xml");
    }
    
    [AllowAnonymous]
    [HttpGet("incoming-call")]
    [HttpPost("incoming-call")]
    public IActionResult HandleIncomingCall([FromForm] TwilioIncomingCallRequest request)
    {
        Log.Information($"Receive twilio incoming call sid: {request.CallSid}");
        var response = new VoiceResponse();
        var connect = new Twilio.TwiML.Voice.Connect();
        
        connect.Stream(url: $"wss://{HttpContext.Request.Host}/api/PhoneOrder/media-stream/{request.CallSid}");
        response.Append(connect);
        return Results.Extensions.TwiML(response);
    }
    
    [AllowAnonymous]
    [HttpGet("media-stream/{callSid}")]
    public async Task HandleMediaStreamAsync(string callSid)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var clientWebSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            
            Log.Information($"Receive incoming call stream sid: {callSid}");
            
            await HandleWebSocketAsync(clientWebSocket, callSid);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }

    private async Task HandleWebSocketAsync(WebSocket twilioWebSocket, string callSid)
    {
        Log.Information("Client connected");

        using var openAiWebSocket = new ClientWebSocket();
        openAiWebSocket.Options.SetRequestHeader("Authorization", $"Bearer {_openAiSettings.ApiKey}");
        openAiWebSocket.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");

        await openAiWebSocket.ConnectAsync(new Uri("wss://api.openai.com/v1/realtime?model=gpt-4o-realtime-preview-2024-12-17"), CancellationToken.None);
        await SendSessionUpdateAsync(openAiWebSocket);
        Log.Information("SendSessionUpdateAsync is successful");
        var context = new StreamContext { CallSid = callSid };

        var receiveFromTwilioTask = ReceiveFromTwilioAsync(twilioWebSocket, openAiWebSocket, context);
        var sendToTwilioTask = SendToTwilioAsync(twilioWebSocket, openAiWebSocket, context);

        try
        {
            await Task.WhenAll(receiveFromTwilioTask, sendToTwilioTask);
        }
        catch (Exception ex)
        {
            Log.Information("Error in one of the tasks: " + ex.Message);
        }
    }

    private async Task ReceiveFromTwilioAsync(WebSocket twilioWebSocket, WebSocket openAiWebSocket, StreamContext context)
    {
        var buffer = new byte[1024 * 10];
        try
        {
            while (twilioWebSocket.State == WebSocketState.Open)
            {
                var result = await twilioWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                // Log.Information("ReceiveFromTwilioAsync result: {result}", Encoding.UTF8.GetString(buffer, 0, result.Count));
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await openAiWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Twilio closed", CancellationToken.None);
                    break;
                }

                if (result.Count > 0)
                {
                    using var jsonDocument = JsonSerializer.Deserialize<JsonDocument>(buffer.AsSpan(0, result.Count));
                    var eventMessage = jsonDocument?.RootElement.GetProperty("event").GetString();
                    
                    switch (eventMessage)
                    {
                        case "connected":
                            break;
                        case "start":
                            context.StreamSid = jsonDocument?.RootElement.GetProperty("start").GetProperty("streamSid").GetString();
                            context.ResponseStartTimestampTwilio = null;
                            context.LatestMediaTimestamp = 0;
                            context.LastAssistantItem = null;
                            break;
                        case "media":
                            var payload = jsonDocument?.RootElement.GetProperty("media").GetProperty("payload").GetString();
                            var audioAppend = new
                            {
                                type = "input_audio_buffer.append",
                                audio = payload
                            };
                            await openAiWebSocket.SendAsync(
                                new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(audioAppend))),
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None
                            );
                            break;
                        case "stop":
                            break;
                    }
                }
            }
        }
        catch (WebSocketException ex)
        {
            Log.Information($"Receive from Twilio error: {ex.Message}");
        }
    }

    private async Task SendToTwilioAsync(WebSocket twilioWebSocket, WebSocket openAiWebSocket, StreamContext context)
    {
        Log.Information("Sending to twilio.");
        var buffer = new byte[1024 * 30];
        try
        {
            while (openAiWebSocket.State == WebSocketState.Open)
            {
                var result = await openAiWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                Log.Information("ReceiveFromOpenAi result: {result}", Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.Count > 0)
                {
                    var jsonDocument = JsonSerializer.Deserialize<JsonDocument>(buffer.AsSpan(0, result.Count));

                    Log.Information($"Received event: {jsonDocument?.RootElement.GetProperty("type").GetString()}");
                    
                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "error" && jsonDocument.RootElement.TryGetProperty("error", out var error))
                    {
                        Log.Information("Receive openai websocket error" + error.GetProperty("message").GetString());
                    }

                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "session.updated")
                    {
                        Log.Information("Session updated successfully");
                    }

                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "response.audio.delta" && jsonDocument.RootElement.TryGetProperty("delta", out var delta))
                    {
                        var audioDelta = new
                        {
                            @event = "media",
                            streamSid = context.StreamSid,
                            media = new { payload = delta.GetString() }
                        };

                        await twilioWebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(audioDelta))), WebSocketMessageType.Text, true, CancellationToken.None);
                        
                        if (context.ResponseStartTimestampTwilio == null)
                        {
                            context.ResponseStartTimestampTwilio = context.LatestMediaTimestamp;
                            if (context.ShowTimingMath)
                            {
                                Log.Information($"Setting start timestamp for new response: {context.ResponseStartTimestampTwilio}ms");
                            }
                        }

                        if (jsonDocument.RootElement.TryGetProperty("item_id", out var itemId))
                        {
                            context.LastAssistantItem = itemId.ToString();
                        }

                        await SendMark(twilioWebSocket, context);
                    }
                    
                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "input_audio_buffer.speech_started")
                    {
                        Log.Information("Speech started detected.");
                        if (!string.IsNullOrEmpty(context.LastAssistantItem))
                        {
                            Log.Information($"Interrupting response with id: {context.LastAssistantItem}");
                            await HandleSpeechStartedEventAsync(twilioWebSocket, openAiWebSocket, context);
                        }
                    }

                    if (jsonDocument?.RootElement.GetProperty("type").GetString() == "response.done")
                    {
                        var response = jsonDocument.RootElement.GetProperty("response");
                        
                        if (response.TryGetProperty("output", out var output) && output.GetArrayLength() > 0)
                        {
                            var firstOutput = output[0];

                            if (firstOutput.GetProperty("type").GetString() == "function_call" && firstOutput.GetProperty("name").GetString() == "transfer_to_human")
                            {
                                await SendTransferringHuman(openAiWebSocket);
                                Log.Information("Sent transfer audio to openai");
                                
                                TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);
                                
                                Log.Information("Connected twilio client");
                                
                                var resource = await CallResource.UpdateAsync(
                                        url: new Uri($"https://{HttpContext.Request.Host.Host}/api/PhoneOrder/transfer.xml"),
                                        pathSid: context.CallSid).ConfigureAwait(false);
                                
                                Log.Information($"Transferred to another number, resource: {@resource}");
                            }
                        }
                    }

                    if (!context.InitialConversationSent)
                    {
                        await SendInitialConversationItem(openAiWebSocket);
                        context.InitialConversationSent = true;
                    }
                }
            }
        }
        catch (WebSocketException ex)
        {
            Log.Information($"Send to Twilio error: {ex.Message}");
        }
    }
    
    private async Task HandleSpeechStartedEventAsync(WebSocket twilioWebSocket, WebSocket openAiWebSocket, StreamContext context)
    {
        Console.WriteLine("Handling speech started event.");
        if (context.MarkQueue.Count > 0 && context.ResponseStartTimestampTwilio.HasValue)
        {
            var elapsedTime = context.LatestMediaTimestamp - context.ResponseStartTimestampTwilio.Value;
            if (context.ShowTimingMath)
            {
                Console.WriteLine($"Calculating elapsed time for truncation: {context.LatestMediaTimestamp} - {context.ResponseStartTimestampTwilio.Value} = {elapsedTime}ms");
            }

            if (!string.IsNullOrEmpty(context.LastAssistantItem))
            {
                if (context.ShowTimingMath)
                {
                    Console.WriteLine($"Truncating item with ID: {context.LastAssistantItem}, Truncated at: {elapsedTime}ms");
                }

                var truncateEvent = new
                {
                    type = "conversation.item.truncate",
                    item_id = context.LastAssistantItem,
                    content_index = 0,
                    audio_end_ms = elapsedTime
                };
                await SendToWebSocketAsync(openAiWebSocket, truncateEvent);
            }

            var clearEvent = new
            {
                Event = "clear",
                context.StreamSid
            };
            
            await SendToWebSocketAsync(twilioWebSocket, clearEvent);

            context.MarkQueue.Clear();
            context.LastAssistantItem = null;
            context.ResponseStartTimestampTwilio = null;
        }
    }

    private async Task SendInitialConversationItem(WebSocket openaiWebSocket)
    {
        var initialConversationItem = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "message",
                role = "user",
                content = new[]
                {
                    new
                    {
                        type = "input_text",
                        text = "Greet the user with: 'Hello Moon house, Santa Monica.'"
                    }
                }
            }
        };

        await SendToWebSocketAsync(openaiWebSocket, initialConversationItem);
        await SendToWebSocketAsync(openaiWebSocket, new { type = "response.create" });
    }
    
    private async Task SendTransferringHuman(WebSocket openaiWebSocket)
    {
        var initialConversationItem = new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "message",
                output = "Tell the customer that you are transferring the call to a real person."
            }
        };

        await SendToWebSocketAsync(openaiWebSocket, initialConversationItem);
        await SendToWebSocketAsync(openaiWebSocket, new { type = "response.create" });
    }
    
    private async Task SendMark(WebSocket twilioWebSocket, StreamContext context)
    {
        if (!string.IsNullOrEmpty(context.StreamSid))
        {
            var markEvent = new
            {
                @event = "mark",
                streamSid = context.StreamSid,
                mark = new { name = "responsePart" }
            };
            await SendToWebSocketAsync(twilioWebSocket, markEvent);
            context.MarkQueue.Enqueue("responsePart");
        }
    }
    
    private async Task SendToWebSocketAsync(WebSocket socket, object message)
    {
        await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message))), WebSocketMessageType.Text, true, CancellationToken.None);
    }
    
    private async Task SendSessionUpdateAsync(WebSocket openAiWebSocket)
    {
        var sessionUpdate = new
        {
            type = "session.update",
            session = new
            {
                turn_detection = new { type = "server_vad" },
                input_audio_format = "g711_ulaw",
                output_audio_format = "g711_ulaw",
                voice = "alloy",
                instructions = SystemMessage,
                modalities = new[] { "text", "audio" },
                temperature = 0.8,
                tool_choice = "auto",
                tools = new[]
                {
                    new
                    {
                        type = "function",
                        name = "transfer_to_human",
                        description = "When the customer explicitly requests that the call be transferred to a real human"
                    }
                }
            }
        };

        await SendToWebSocketAsync(openAiWebSocket, sessionUpdate);
    }
    
    public class StreamContext
    {
        public string? StreamSid { get; set; }

        public int LatestMediaTimestamp { get; set; } = 0;
        
        public string? LastAssistantItem { get; set; }
        
        public Queue<string> MarkQueue = new Queue<string>();

        public long? ResponseStartTimestampTwilio { get; set; } = null;
        
        public bool InitialConversationSent { get; set; } = false;

        public bool ShowTimingMath { get; set; } = false;
        
        public string CallSid { get; set; }
    }
    
    public class TwilioIncomingCallRequest
    {
        public string CallSid { get; set; }
    }
}