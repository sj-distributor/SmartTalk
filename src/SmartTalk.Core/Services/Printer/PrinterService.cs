using System.Reflection;
using SmartTalk.Core.Domain.Printer;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Caching;
using SmartTalk.Messages.Requests.Printer;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;
using System.Text;
using System.Text.RegularExpressions;
using Aliyun.OSS;
using AutoMapper;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Services.AliYun;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Settings.Printer;
using SmartTalk.Message.Commands.Printer;
using SmartTalk.Message.Events.Printer;
using SmartTalk.Messages.Commands.Printer;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.Printer;
using SmartTalk.Messages.Dto.WeChat;
using SmartTalk.Messages.Enums.Printer;
using SmartTalk.Messages.Events.Printer;
using Color = SixLabors.ImageSharp.Color;

namespace SmartTalk.Core.Services.Printer;

public interface IPrinterService : IScopedDependency
{
    Task<GetPrinterJobAvailableResponse> GetPrinterJobAvailableAsync(GetPrinterJobAvailableRequest request,
        CancellationToken cancellationToken);
    
    Task<string> UploadOrderPrintImageAndUpdatePrintUrlAsync(Guid jobToken, DateTimeOffset printDate, CancellationToken cancellationToken);
    
    Task<PrinterJobResponse> PrinterJobAsync(PrinterJobCommand command, CancellationToken cancellationToken);
    
    Task<PrinterJobConfirmedEvent> ConfirmPrinterJobAsync(ConfirmPrinterJobCommand command,
        CancellationToken cancellationToken);
    
    Task<PrinterJobConfirmedEvent> RecordPrintErrorAfterConfirmPrinterJobAsync(ConfirmPrinterJobCommand command,
        CancellationToken cancellationToken);
    
    Task<PrinterStatusChangedEvent> RecordPrinterStatusAsync(RecordPrinterStatusCommand command,
        CancellationToken cancellationToken);
    
    Task<PrintTestResponse> PrintTestAsync(PrintTestCommand command, CancellationToken cancellationToken);

    Task PrinterStatusChangedAsync(PrinterStatusChangedEvent @event, CancellationToken cancellationToken);

    Task PrinterJobConfirmeAsync(PrinterJobConfirmedEvent @event, CancellationToken cancellationToken);
    
    Task<AddMerchPrinterResponse> AddMerchPrinterAsync(AddMerchPrinterCommand command, CancellationToken cancellationToken);
    
    Task<GetMerchPrintersResponse> GetMerchPrintersAsync(GetMerchPrintersRequest request, CancellationToken cancellationToken);
    
    Task<DeleteMerchPrinterResponse> DeleteMerchPrinterAsync(DeleteMerchPrinterCommand command, CancellationToken cancellationToken);
    
    Task<UpdateMerchPrinterResponse> UpdateMerchPrinterAsync(UpdateMerchPrinterCommand command, CancellationToken cancellationToken);
    
    Task<GetMerchPrinterLogResponse> GetMerchPrinterLogAsync(GetMerchPrinterLogRequest request, CancellationToken cancellationToken);
    
    Task ScanOfflinePrinter(CancellationToken cancellationToken);
}

public class PrinterService : IPrinterService
{
    private readonly IMapper _mapper;
    private readonly IWeChatClient _weChatClient;
    private readonly ICacheManager _cacheManager;
    private readonly IAliYunOssService _ossService;
    private readonly IPosDataProvider _posDataProvider;
    private readonly IPrinterDataProvider _printerDataProvider;
    private readonly PrinterSendErrorLogSetting _printerSendErrorLogSetting;

    public PrinterService(IMapper mapper, IWeChatClient weChatClient, ICacheManager cacheManager, IAliYunOssService ossService, IPosDataProvider posDataProvider, IPrinterDataProvider printerDataProvider, PrinterSendErrorLogSetting printerSendErrorLogSetting)
    {
        _mapper = mapper;
        _ossService = ossService;
        _weChatClient = weChatClient;
        _cacheManager = cacheManager;
        _posDataProvider = posDataProvider;
        _printerDataProvider = printerDataProvider;
        _printerSendErrorLogSetting = printerSendErrorLogSetting;
    }

    public async Task<GetPrinterJobAvailableResponse> GetPrinterJobAvailableAsync(GetPrinterJobAvailableRequest request, CancellationToken cancellationToken)
    {
        var key = $"{request.PrinterMac}-{request.Token}";
        
        Log.Information($"key: {key}", key);
        
        if (await _cacheManager.GetAsync<object>(key, new RedisCachingSetting(), cancellationToken).ConfigureAwait(false) != null)
        {
            return null;
        }

        var merchPrinter = (await _printerDataProvider.GetMerchPrintersAsync(request.PrinterMac, request.Token, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        Log.Information("MerchPrinter: {@merchPrinter}", merchPrinter);
        
        if (merchPrinter == null)
        {
            await _cacheManager.SetAsync(key,1, new RedisCachingSetting(expiry: TimeSpan.FromMinutes(2)),cancellationToken).ConfigureAwait(false);
            return null; 
        }
            
        var merchPrinterJob = await GetMerchPrinterJobAsync(merchPrinter, cancellationToken).ConfigureAwait(false);

        return new GetPrinterJobAvailableResponse()
        {
            JobReady = merchPrinterJob != null,
            JobToken = merchPrinterJob?.Id,
        };
    }
    
    private async Task<MerchPrinterOrder> GetMerchPrinterJobAsync(MerchPrinter merchPrinter, CancellationToken cancellationToken)
    {
        var store = await _posDataProvider.GetPosCompanyStoreDetailAsync(merchPrinter.StoreId, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Store: {@agent}", store);
        
        if (store is null) return null;
        
        if (!merchPrinter.IsEnabled)
            return (await _printerDataProvider.GetMerchPrinterOrdersAsync(null, merchPrinter.StoreId,  PrintStatus.Waiting, DateTimeOffset.Now, merchPrinter.PrinterMac, true, cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        var merchPrinterOrders = await  _printerDataProvider.GetMerchPrinterOrdersAsync(null, merchPrinter.StoreId,  PrintStatus.Waiting, DateTimeOffset.Now, null, true, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        foreach (var merchPrinterOrder in merchPrinterOrders)
        {
            if (string.IsNullOrEmpty(merchPrinterOrder.PrinterMac) || merchPrinterOrder.PrinterMac == merchPrinter.PrinterMac)
                return merchPrinterOrder;
        }

        return null;
    }

    public async Task<string> UploadOrderPrintImageAndUpdatePrintUrlAsync(
        Guid jobToken, DateTimeOffset printDate, CancellationToken cancellationToken)
    {
        var merchPrinterOrder = (await _printerDataProvider.GetMerchPrinterOrdersAsync(jobToken, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        if (merchPrinterOrder == null) return string.Empty;
        
        if (!string.IsNullOrEmpty(merchPrinterOrder.ImageUrl)) return merchPrinterOrder.ImageUrl;

        var order = await _posDataProvider.GetPosOrderByIdAsync(orderId: merchPrinterOrder.OrderId, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var orderItems = JsonConvert.DeserializeObject<List<PhoneCallOrderItem>>(order.Items);

        var productIds = new List<string>();
        
        foreach (var orderItem in orderItems)
        {
            productIds.Add(orderItem.ProductId.ToString());
        }

        if (productIds.Count > 0)
        {
            var products = await _posDataProvider.GetPosProductsAsync(productIds: productIds, cancellationToken: cancellationToken).ConfigureAwait(false);

            Log.Information("Products:{@products}", products);
            
            foreach (var orderItem in orderItems)
            {
                var product = products.FirstOrDefault(x => x.ProductId == orderItem.ProductId.ToString());

                if (product != null)
                    orderItem.ProductName = product.Names;
            }
        }

        var merchPrinter = (await _printerDataProvider.GetMerchPrintersAsync(printerMac: merchPrinterOrder.PrinterMac,
            storeId: merchPrinterOrder.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        
        var store = await _posDataProvider.GetPosCompanyStoreDetailAsync(order.StoreId, cancellationToken).ConfigureAwait(false);

        var storeCreatedDate = "";
        var storePrintDate = "";
        if (!string.IsNullOrEmpty(store.Timezone))
        {
            storeCreatedDate = TimeZoneInfo.ConvertTimeFromUtc(order.CreatedDate.UtcDateTime, TimeZoneInfo.FindSystemTimeZoneById(store.Timezone)).ToString("yyyy-MM-dd HH:mm:ss");    
            storePrintDate = TimeZoneInfo.ConvertTimeFromUtc(merchPrinterOrder.PrintDate.UtcDateTime, TimeZoneInfo.FindSystemTimeZoneById(store.Timezone)).ToString("yyyy-MM-dd HH:mm:ss");    
        }
        else
        {
            storeCreatedDate = order.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss");
            storePrintDate = merchPrinterOrder.PrintDate.ToString("yyyy-MM-dd HH:mm:ss");
        }
        
        var img = await RenderReceiptAsync(order.OrderNo,  
            JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(store.Names).GetValueOrDefault("en")?.GetValueOrDefault("name"),
            store.Address,
            store.PhoneNums,
            storeCreatedDate,
            merchPrinter.PrinterName,
            order.Type.ToString(),
            order.Name,
            order.Phone,
            order.Address,
            order.Notes,
            orderItems,
            order.SubTotal.ToString(),
            order.Tax.ToString(),
            order.Total.ToString(),
            storePrintDate,
            merchPrinter.PrinterLanguage).ConfigureAwait(false);
       
        var imageKey = Guid.NewGuid().ToString();
        
        using var ms = new MemoryStream();
        img.SaveAsJpeg(ms);
        ms.Position = 0;

        var metadata = new ObjectMetadata
        {
            ContentType = "image/jpeg"
        };
        
        _ossService.UploadFile(imageKey, ms.ToArray(), metadata);
        
        merchPrinterOrder.ImageKey = imageKey;
        merchPrinterOrder.ImageUrl = _ossService.GetFileUrl(imageKey);

        Log.Information(@"MerchPrinterOrder image key: {ImageKey}, url: {ImageUrl}", merchPrinterOrder.ImageKey,  merchPrinterOrder.ImageUrl);
        
        await _printerDataProvider.UpdateMerchPrinterOrderAsync(merchPrinterOrder, cancellationToken: cancellationToken).ConfigureAwait(false);

        return merchPrinterOrder.ImageUrl;
    }

    public async Task<PrinterJobResponse> PrinterJobAsync(PrinterJobCommand command, CancellationToken cancellationToken)
    {
        Log.Information("PrinterJobCommand: {@PrinterJobCommand}", command);
        
        var merchPrinterOrder = (await _printerDataProvider.GetMerchPrinterOrdersAsync(command.JobToken, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        
        if (merchPrinterOrder != null && merchPrinterOrder.PrintStatus == PrintStatus.Waiting)
        {
            merchPrinterOrder.PrintStatus = PrintStatus.Printing;
            await _printerDataProvider.UpdateMerchPrinterOrderAsync(merchPrinterOrder, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return new PrinterJobResponse
        {
            MerchPrinterOrder = _mapper.Map<MerchPrinterOrderDto>(merchPrinterOrder)
        };
    }
    
    public async Task<PrinterJobConfirmedEvent> ConfirmPrinterJobAsync(ConfirmPrinterJobCommand command,
        CancellationToken cancellationToken)
    {
        var merchPrinterOrder = (await _printerDataProvider.GetMerchPrinterOrdersAsync(
            jobToken: command.JobToken , cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        
        if (merchPrinterOrder != null && merchPrinterOrder.PrintStatus == PrintStatus.Printing)
        {
            merchPrinterOrder.PrintStatus = PrintStatus.Printed;
           
            await _printerDataProvider.UpdateMerchPrinterOrderAsync(merchPrinterOrder, cancellationToken: cancellationToken).ConfigureAwait(false);

            return new PrinterJobConfirmedEvent
            {
                MerchPrinterOrderDto = _mapper.Map<MerchPrinterOrderDto>(merchPrinterOrder),
                PrinterMac = command.PrinterMac,
                PrinterStatusCode = command.PrintStatusCode
            };
        }

        return null;
    }
    
    public async Task<PrinterJobConfirmedEvent> RecordPrintErrorAfterConfirmPrinterJobAsync(
        ConfirmPrinterJobCommand command, CancellationToken cancellationToken)
    {
        var merchPrinterOrder = (await _printerDataProvider.GetMerchPrinterOrdersAsync(
            jobToken: command.JobToken , cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        
        if (merchPrinterOrder != null && merchPrinterOrder.PrintStatus == PrintStatus.Printing)
        {
            if (merchPrinterOrder.PrintErrorTimes < 5)
                merchPrinterOrder.PrintStatus = PrintStatus.Waiting;

            merchPrinterOrder.PrintErrorTimes++;
             
            await _printerDataProvider.UpdateMerchPrinterOrderAsync(merchPrinterOrder, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            return new PrinterJobConfirmedEvent
            {
                MerchPrinterOrderDto = _mapper.Map<MerchPrinterOrderDto>(merchPrinterOrder),
                PrinterMac = command.PrinterMac,
                PrinterStatusCode = command.PrintStatusCode
            };
        }

        return null;
    }
    
    public async Task<PrinterStatusChangedEvent> RecordPrinterStatusAsync(
        RecordPrinterStatusCommand command, CancellationToken cancellationToken)
    {
        var merchPrinter = (await _printerDataProvider.GetMerchPrintersAsync(
            command.PrinterMac, command.Token, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        if (merchPrinter == null)
            return null;

        var @event = new PrinterStatusChangedEvent()
        {
            PrinterMac = command.PrinterMac,
            Token = command.Token,
            OldPrinterStatusInfo = string.IsNullOrWhiteSpace(merchPrinter.StatusInfo)
                ? null
                : JsonConvert.DeserializeObject<PrinterStatusInfo>(merchPrinter.StatusInfo),
            NewPrinterStatusInfo = command,
        };

        merchPrinter.StatusInfo = JsonConvert.SerializeObject(command);
        merchPrinter.StatusInfoLastModifiedDate = DateTimeOffset.Now;

        await _printerDataProvider.UpdateMerchPrinterMacAsync(merchPrinter, cancellationToken: cancellationToken).ConfigureAwait(false);

        return @event;
    }

    public async Task<PrintTestResponse> PrintTestAsync(PrintTestCommand command, CancellationToken cancellationToken)
    {
        var merchPrinterOrder = new MerchPrinterOrder
        {
            OrderId = 0,
            StoreId = command.StoreId,
            PrinterMac = command.PrinterMac,
            PrintDate = DateTimeOffset.Now
        };
        
        await _printerDataProvider.AddMerchPrinterOrderAsync(merchPrinterOrder, cancellationToken).ConfigureAwait(false);

        return new PrintTestResponse();
    }

    public async Task PrinterStatusChangedAsync(PrinterStatusChangedEvent @event, CancellationToken cancellationToken)
    {
        if (@event.Skip())
            return;
        
        var merchPrinterLog = await GenerateMerchPrinterLogAsync(@event, cancellationToken).ConfigureAwait(false);
        
        if (merchPrinterLog != null)
            await _printerDataProvider.AddMerchPrinterLogAsync(merchPrinterLog, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MerchPrinterLog> GenerateMerchPrinterLogAsync(PrinterStatusChangedEvent @event, CancellationToken cancellationToken)
    {
        var varianceList = GetVarianceList(@event);
        
        Log.Information("VarianceList:{@varianceList}", varianceList);
        
        if (!varianceList.Any()) return null;

        var merchPrinter = (await _printerDataProvider.GetMerchPrintersAsync(@event.PrinterMac, @event.Token, cancellationToken: cancellationToken)).FirstOrDefault();
        
        Log.Information("Log merch printer:{@merchPrinter}", merchPrinter);
        
        if (merchPrinter == null)
            return null;

        return new MerchPrinterLog
        {
            Id = Guid.NewGuid(),
            StoreId = merchPrinter.StoreId,
            OrderId = null,
            PrinterMac = @event.PrinterMac,
            PrintLogType = PrintLogType.StatusChange,
            Message = string.Join(Environment.NewLine, varianceList.Select(v => v.ToString()))
        };
    }
    
    private static readonly PropertyInfo[]  PrinterStatusPropertyInfos = typeof(PrinterStatusInfo).GetProperties();

    private static List<Variance> GetVarianceList(PrinterStatusChangedEvent @event)
    {
        var variances = new List<Variance>();
        var oldInfo = @event.OldPrinterStatusInfo;
        var newInfo = @event.NewPrinterStatusInfo;

        foreach (var propertyInfo in PrinterStatusPropertyInfos)
        {
            var variance = new Variance
            {
                Name = propertyInfo.Name, OldValue = (bool) propertyInfo.GetValue(oldInfo), NewValue = (bool) propertyInfo.GetValue(newInfo)
            };

            if (variance.OldValue != variance.NewValue)
                variances.Add(variance);
        }

        return variances;
    }

    private class Variance
    {
        public string Name { get; set; }
        public bool OldValue { get; set; }
        public bool NewValue { get; set; }

        public override string ToString()
        {
            return $"{Name}:{OldValue}->{NewValue}";
        }
    }

    public async Task PrinterJobConfirmeAsync(PrinterJobConfirmedEvent @event, CancellationToken cancellationToken)
    {
        var merchPrinterLog = _mapper.Map<MerchPrinterLog>(@event);
        var printError = @event.IsPrintError();
        
        var message = $"{(printError?"Print Error":"Print")}";
        merchPrinterLog.Message = message;
        
        if (message.Equals("Print Error"))
        {
            var store = await _posDataProvider.GetPosCompanyStoreAsync(id: @event.MerchPrinterOrderDto.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);

            var order = await _posDataProvider.GetPosOrderByIdAsync(orderId: @event.MerchPrinterOrderDto.OrderId, cancellationToken: cancellationToken).ConfigureAwait(false);
        
            var storeName = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(store.Names).GetValueOrDefault("en")?.GetValueOrDefault("name");
        
            var text = new SendWorkWechatGroupRobotTextDto { Content = $"🆘SMT Cloud Print Error Infor🆘\n\nPrint Error: {merchPrinterLog.Message}\nPrint Time: {@event.MerchPrinterOrderDto.PrintDate.ToString("yyyy-MM-dd HH:mm:ss")}\nStore: {storeName}\nOrder Date:{order.CreatedDate.ToString("yyyy-MM-dd")}\nOrder NO: #{order.OrderNo}"};
            text.MentionedMobileList = "@all";
        
            await _weChatClient.SendWorkWechatRobotMessagesAsync(_printerSendErrorLogSetting.CloudPrinterSendErrorLogRobotUrl, new SendWorkWechatGroupRobotMessageDto
            {
                MsgType = "text",
                Text = text
            }, cancellationToken).ConfigureAwait(false);
        }
        
        await _printerDataProvider.AddMerchPrinterLogAsync(merchPrinterLog, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Image<Rgba32>> RenderReceiptAsync(string printNumber, string restaurantName, string restaurantAddress,
        string restaurantPhone, string orderTime, string printerName, string orderType, string guestName, 
        string guestPhone, string guestAddress, string orderNotes, List<PhoneCallOrderItem> orderItems,
        string subtotal, string tax, string total, string printTime, PrinterLanguageType? printerLanguageType)
    {
        var y = 10;
        var paperWidth = 512;
        var textColor = Color.Black;
        var bgColor = Color.White;
        
        var regularFont = "/app/fonts/SourceHanSansSC-Regular.otf";
        var boldFont = "/app/fonts/SourceHanSansSC-Bold.otf";
        
        var collection = new FontCollection();
        var fontFamily = collection.Add(regularFont);
        collection.Add(boldFont);
        
        var fontSmall = CreateFont(fontFamily, 25);
        var fontMaxSmall = CreateFont(fontFamily, 23);
        var fontNormal = CreateFont(fontFamily, 30);
        var fontBold = CreateFont(fontFamily, 40, bold: true);
        
        var lineHeight = TextMeasurer.MeasureSize("口", new TextOptions(fontNormal)).Height;

        var img = new Image<Rgba32>(paperWidth, 10000);
        img.Mutate(ctx => ctx.Fill(bgColor));
        
        Log.Information("orderItems: {@orderItems}", orderItems);
        
        y = DrawLine(paperWidth, img, y, textColor, $"#{printNumber}", fontNormal);
        y = DrawLine(paperWidth, img, y, textColor, $"{orderType} Order",  CreateFont(fontFamily, 45, true), spacing: 40, centerAlign: true);
        y = DrawLine(paperWidth, img, y, textColor, $"{restaurantName}", fontMaxSmall, centerAlign: true);
        
        if (!string.IsNullOrEmpty(restaurantAddress))
            y = DrawLine(paperWidth, img, y, textColor, $"{restaurantAddress}", fontMaxSmall, centerAlign: true);

        if (!string.IsNullOrEmpty(restaurantPhone))
        {
            var phoneSplit = restaurantPhone.Split(",");

            var phones = "";
            
            foreach (var phone in phoneSplit)
            {
                phones += $"({phone[..3]})-{phone.Substring(3, 3)}-{phone[6..]},";
            }
            
            phones = phones.TrimEnd(',');
            
            y = DrawLine(paperWidth, img, y, textColor, $"{phones}", fontMaxSmall, centerAlign: true, spacing:10);
        }
        
        y = DrawLine(paperWidth, img, y, textColor, GenerateFullLine('-', fontBold, paperWidth-20), fontBold, yOffset: -10);
        
        y = DrawItemLine(paperWidth, img, y, textColor, lineHeight, $"{orderTime}", $"{orderType}", fontSmall);
        y = DrawItemLine(paperWidth, img, y, textColor, lineHeight, $"{printerName}", "AiPhoneOrder", fontSmall);
        
        y = DrawSolidLine(paperWidth, img, y);

        if (!string.IsNullOrEmpty(guestName))
            y = DrawLine(paperWidth, img, y, textColor, $"{guestName}", fontSmall);

        if (!string.IsNullOrEmpty(guestPhone))
        {
            var formatted = $"({guestPhone[..3]})-{guestPhone.Substring(3, 3)}-{guestPhone[6..]}";
            y = DrawLine(paperWidth, img, y, textColor, $"{formatted}", fontSmall);    
        }
        
        if (!string.IsNullOrEmpty(guestAddress))
            y = DrawLine(paperWidth, img, y, textColor, $"{guestAddress}", fontSmall);

        if (!string.IsNullOrEmpty(orderNotes))
        {
            y = DrawSolidLine(paperWidth, img, y);
            y = DrawLine(paperWidth, img, y, textColor, $"Order Remark:{orderNotes}", fontNormal);
        }
        
        y = DrawLine(paperWidth, img, y, textColor,GenerateFullLine('-', fontNormal, paperWidth-20), fontNormal, yOffset: -10);
        
        y = DrawingOrderItems(fontFamily, paperWidth, img, y, textColor, orderItems, printerLanguageType);
        
        y = DrawLine(paperWidth, img, y, textColor,GenerateFullLine('-', fontNormal, paperWidth-20), fontNormal, yOffset: -15);
        
        y = DrawItemLine(paperWidth, img, y, textColor, lineHeight, "Subtotal", $"${subtotal}", fontSmall);
        y = DrawItemLine(paperWidth, img, y, textColor, lineHeight, "Tax", $"${tax}", fontSmall);
        y = DrawItemLine(paperWidth, img, y, textColor, lineHeight, "Total", $"${total}", fontNormal);
        
        y = DrawLine(paperWidth, img, y, textColor,GenerateFullLine('-', fontNormal, paperWidth-20), fontNormal);
        
        y = DrawLine(paperWidth, img, y, textColor, "*** Unpaid ***", CreateFont(fontFamily, 35, true), spacing: 15, centerAlign: true, yOffset: 15);
        
        y = DrawLine(paperWidth, img, y, textColor,GenerateFullLine('-', fontNormal, paperWidth-20), fontNormal);
        
        y = DrawLine(paperWidth, img, y, textColor, $"Print Time {printTime}", fontSmall, centerAlign: true, spacing: 80, yOffset: 60);
        y = DrawLine(paperWidth, img, y, textColor, $"Powered by SmartTalk AI", fontSmall, centerAlign: true);

        img.Mutate(x => x.Crop(new Rectangle(0, 0, paperWidth, y + 10)));
        
        return img;
    } 
    
    public async Task<GetMerchPrintersResponse> GetMerchPrintersAsync(GetMerchPrintersRequest request, CancellationToken cancellationToken)
    {
        var printers = await _printerDataProvider.GetMerchPrintersAsync(
            storeId: request.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);

        var result = _mapper.Map<List<MerchPrinterDto>>(printers);

        return new GetMerchPrintersResponse
        {
            Data = result
        };
    }
    
    public async Task<AddMerchPrinterResponse> AddMerchPrinterAsync(AddMerchPrinterCommand command, CancellationToken cancellationToken)
    {
        var printer = (await _printerDataProvider.GetMerchPrintersAsync(printerMac: command.PrinterMac, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        if (printer != null)
            throw new Exception($"The printer [{command.PrinterMac}] already exists");

        printer = _mapper.Map<MerchPrinter>(command);
        printer.Token = await GetOrCreatePrinterTokenAsync(command.PrinterMac, cancellationToken).ConfigureAwait(false);

        await CheckPrinterCanOnlyHaveOneEnabled(printer, cancellationToken);

        await _printerDataProvider.AddMerchPrinterAsync(printer, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new AddMerchPrinterResponse();
    }

    public async Task<DeleteMerchPrinterResponse> DeleteMerchPrinterAsync(DeleteMerchPrinterCommand command, CancellationToken cancellationToken)
    {
        var printer = (await _printerDataProvider.GetMerchPrintersAsync(id: command.Id, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        
       await _printerDataProvider.DeleteMerchPrinterAsync(printer, cancellationToken: cancellationToken).ConfigureAwait(false);

       return new DeleteMerchPrinterResponse();
    }

    private async Task<Guid> GetOrCreatePrinterTokenAsync(string printerMac,CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(printerMac))
            throw new ArgumentNullException(nameof(printerMac));
            
        var printerToken = await _printerDataProvider.GetPrinterTokenAsync(printerMac, cancellationToken).ConfigureAwait(false);

        if (printerToken != null)
            return printerToken.Token;

        printerToken = new PrinterToken()
        {
            Id = Guid.NewGuid(),
            PrinterMac = printerMac,
            Token = Guid.NewGuid(),
        };

        await _printerDataProvider.AddPrinterTokenAsync(printerToken, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        return printerToken.Token;
    }
    
    private async Task CheckPrinterCanOnlyHaveOneEnabled(MerchPrinter merchPrinter, CancellationToken cancellationToken)
    {
        if (merchPrinter.IsEnabled)
        {
            var enabledPrinters = await _printerDataProvider.GetMerchPrintersAsync(
                storeId: merchPrinter.StoreId, id: merchPrinter.Id, isEnabled: true, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (enabledPrinters.Count > 0)
            {
                throw new Exception(
                    "Only one device can be activated. Please disable the current printer before activating this printer.");
            }
        }
    }

    public async Task<UpdateMerchPrinterResponse> UpdateMerchPrinterAsync(UpdateMerchPrinterCommand command, CancellationToken cancellationToken)
    {
        var printer = (await _printerDataProvider.GetMerchPrintersAsync(id: command.Id, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        _mapper.Map(command, printer);

        await CheckPrinterCanOnlyHaveOneEnabled(printer, cancellationToken).ConfigureAwait(false);
        
        await _printerDataProvider.UpdateMerchPrinterMacAsync(printer, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new UpdateMerchPrinterResponse();
    }
    
     public async Task<GetMerchPrinterLogResponse> GetMerchPrinterLogAsync(GetMerchPrinterLogRequest request, CancellationToken cancellationToken)
     {
         var (count, merchPrinterLogs) = await _printerDataProvider.GetMerchPrinterLogAsync(
             request.StoreId,request.PrinterMac, request.StartDate, request.EndDate, request.Code, request.PrintLogType,
             request.PageIndex, request.PageSize, cancellationToken).ConfigureAwait(false);

         return new GetMerchPrinterLogResponse 
         {
             Data = new MerchPrinterLogCountDto 
             { 
                 TotalCount = count, 
                 MerchPrinterLogDtos = merchPrinterLogs
             }
         };
     }

     public async Task ScanOfflinePrinter(CancellationToken cancellationToken)
     {
         var allMaybeOfflinePrinters = await _printerDataProvider.GetMerchPrintersAsync(isEnabled: true,
             lastStatusInfoLastModifiedDate: DateTimeOffset.Now.AddMinutes(-2), IsStatusInfo: true,
             cancellationToken: cancellationToken).ConfigureAwait(false);
         
         foreach (var printer in allMaybeOfflinePrinters)
         {
             printer.StatusInfo = null;
             await _printerDataProvider.UpdateMerchPrinterMacAsync(printer, cancellationToken: cancellationToken).ConfigureAwait(false);
         }
     } 
     
     private static Font CreateFont(FontFamily fontFamily, float size, bool bold = false)
     {
         return new Font(fontFamily, size, bold ? FontStyle.Bold : FontStyle.Regular);
     }
     
     private static int DrawItemLineThreeColsWrapped(FontFamily fontFamily, int paperWidth, Image<Rgba32> img, int y, Color textColor
         , string qty, OrderItemsDto itemName, string price, List<OrderItemsDto> remarks = null
         , float fontSize = 27, bool bold = false, float lineSpacing = 1.6f, float remarkLineSpacing = 1.6f, int backSpacing = 20)
     {
         var font = new Font(fontFamily, fontSize, bold ? FontStyle.Bold : FontStyle.Regular);
         var boldFont = new Font(fontFamily, fontSize, FontStyle.Bold);
         var remarkFont = new Font(fontFamily, fontSize - 3, FontStyle.Regular);

         var padding = 10;
         var col1Width = 80;
         var col3Width = 150;
         var col2Width = paperWidth - col1Width - col3Width - padding * 2;

         var col1X = padding;
         var col2X = col1X + col1Width;
         var col3X = paperWidth - col3Width - padding;

         var baseOptions = new TextOptions(font)
         {
             WrappingLength = col2Width,
             LineSpacing = lineSpacing,
             HorizontalAlignment = HorizontalAlignment.Left,
             VerticalAlignment = VerticalAlignment.Top
         };

         float indentX = 0;
         float itemBlockHeight = 0;
         var firstChar = "";
         var towFontWidth = TextMeasurer.MeasureSize("口口", baseOptions).Width;

         if (itemName != null)
         {
             if (!string.IsNullOrEmpty(itemName.EnName) && string.IsNullOrEmpty(itemName.CnName))
                firstChar = itemName.EnName;
             
             if (!string.IsNullOrEmpty(itemName.CnName) && string.IsNullOrEmpty(itemName.EnName)) 
                 firstChar = itemName.CnName;

             if (!string.IsNullOrEmpty(itemName.EnName) && !string.IsNullOrEmpty(itemName.CnName))
                 firstChar = itemName.EnName + "\n" + itemName.CnName;
             
             var itemTextOptions = new RichTextOptions(font)
             {
                 Origin = new PointF(col2X, y), WrappingLength = col2Width, LineSpacing = lineSpacing
             };
             
             var size = TextMeasurer.MeasureSize(firstChar, itemTextOptions);
             itemBlockHeight = size.Height;
             indentX = size.Width;
         }

         List<(string prefix, string content)> remarkLines = [];

         Log.Information("Remarks:{@remarks}", remarks);

         var remarkExtraWidth = 50;

         if (remarks != null)
         {
             foreach (var remark in remarks)
             {
                 var content = "";
                 
                 if (!string.IsNullOrEmpty(remark.EnName) && string.IsNullOrEmpty(remark.CnName))
                     content = remark.EnName;
             
                 if (!string.IsNullOrEmpty(remark.CnName) && string.IsNullOrEmpty(remark.EnName)) 
                     content = remark.CnName;

                 if (!string.IsNullOrEmpty(remark.EnName) && !string.IsNullOrEmpty(remark.CnName))
                     content = remark.EnName + "\n" + remark.CnName;
                 
                 var prefix = remark.Count > 1 ? $"{remark.Count}" : ">";
                 remarkLines.Add((prefix, content));
             }
         }

         var remarkBlockHeight = 0f;

         foreach (var (prefix, content) in remarkLines)
         {
             var prefixWidth = TextMeasurer.MeasureSize(prefix, new TextOptions(font)).Width;
             var spaceWidth = TextMeasurer.MeasureSize(" ", new TextOptions(font)).Width;
             var contentX = col2X + towFontWidth + prefixWidth + spaceWidth * 4;

             var contentOptions = new RichTextOptions(remarkFont)
             {
                 WrappingLength = col2Width + remarkExtraWidth - (contentX - col2X),
                 LineSpacing = remarkLineSpacing
             };

             var remarkLineHeight = TextMeasurer.MeasureSize(content.TrimStart(), contentOptions).Height;
             
             remarkBlockHeight += remarkLineHeight;
         }

         var totalBlockHeight = itemBlockHeight + (remarkBlockHeight > 0 ? remarkBlockHeight : 0);
         float remarkY;
         var remarkSpacer = 10;
         img.Mutate(ctx =>
         {
             var qtyTextOptions = new RichTextOptions(boldFont)
             {
                 Origin = new PointF(col1X, y), WrappingLength = col2Width, LineSpacing = lineSpacing
             };
             ctx.DrawText(qtyTextOptions, qty, textColor);

             var priceSize = TextMeasurer.MeasureSize(price, new TextOptions(font));
             var priceX = col3X + col3Width - priceSize.Width;

             var priceTextOptions = new RichTextOptions(boldFont)
             {
                 Origin = new PointF(priceX, y), WrappingLength = col2Width, LineSpacing = lineSpacing
             };

             ctx.DrawText(priceTextOptions, price, textColor);

             var itemTextOptions = new RichTextOptions(font)
             {
                 Origin = new PointF(col2X, y), WrappingLength = col2Width, LineSpacing = lineSpacing
             };

             ctx.DrawText(itemTextOptions, firstChar, textColor);

             remarkY = y + itemBlockHeight + remarkSpacer;
             foreach (var (prefix, content) in remarkLines)
             {
                 var prefixOptions = new RichTextOptions(remarkFont)
                 {
                     Origin = new PointF(col2X + towFontWidth, remarkY),
                     WrappingLength = col2Width + remarkExtraWidth - indentX,
                     LineSpacing = remarkLineSpacing
                 };
                 ctx.DrawText(prefixOptions, prefix, textColor);

                 var prefixWidth = TextMeasurer.MeasureSize(prefix, new TextOptions(font)).Width;
                 var spaceWidth = TextMeasurer.MeasureSize(" ", new TextOptions(font)).Width;
                 var contentX = col2X + towFontWidth + prefixWidth + spaceWidth * 4;

                 var contentOptions = new RichTextOptions(remarkFont)
                 {
                     Origin = new PointF(contentX, remarkY),
                     WrappingLength = col2Width + remarkExtraWidth - (contentX - col2X),
                     LineSpacing = remarkLineSpacing
                 };
                 ctx.DrawText(contentOptions, content.TrimStart(), textColor);

                 var remarkLineHeight = TextMeasurer.MeasureSize(content.TrimStart(), contentOptions).Height;
                 remarkY += remarkLineHeight + 10;
                 remarkSpacer += 10;
             }
         });

         y += (int)totalBlockHeight + backSpacing + remarkSpacer;

         return y;
     }

     private static int DrawingOrderItems(FontFamily fontFamily, int paperWidth, Image<Rgba32> img, int y,
         Color textColor, List<PhoneCallOrderItem> orderItems, PrinterLanguageType? printerLanguageType)
     {
         y = DrawItemLineThreeColsWrapped(fontFamily, paperWidth, img, y, textColor, "QTY",
             new OrderItemsDto { EnName = "Items" }, "Total", fontSize: 25, bold: true);

         foreach (var orderItem in orderItems)
         {
             var itema = new OrderItemsDto()
             {
                 EnName = IsGetLanguageValue(printerLanguageType, "en") ? orderItem.ProductNames.GetValueOrDefault("en")?.GetValueOrDefault("posName") : null,
                 CnName = IsGetLanguageValue(printerLanguageType, "cn") ? orderItem.ProductNames.GetValueOrDefault("cn")?.GetValueOrDefault("posName") : null,
             };

             decimal itembMoney = 0;
             var itemb = orderItem.OrderItemModifiers.Select(x =>
                 {
                     itembMoney += x.Price * x.Quantity * orderItem.Quantity;

                     return new OrderItemsDto()
                     {
                         Count = x.Quantity,
                         EnName = IsGetLanguageValue(printerLanguageType, "en") ? x.ModifierLocalizations.FirstOrDefault(s => s.Field == "name" && s.LanguageCode == "en_US")?.Value + "($" + (x.Price*x.Quantity).ToString("0.00") + ")" : null,
                         CnName = IsGetLanguageValue(printerLanguageType, "cn") ? x.ModifierLocalizations.FirstOrDefault(s => s.Field == "name" && s.LanguageCode == "zh_CN")?.Value + "($" + (x.Price*x.Quantity).ToString("0.00") + ")" : null
                     };
                 })
                 .ToList();

             if (!string.IsNullOrEmpty(orderItem.Notes))
             {
                 var item = new OrderItemsDto();
                 
                 if (printerLanguageType != PrinterLanguageType.EnglishAndChinese)
                 {
                     item = IsGetLanguageValue(printerLanguageType, "en")
                         ? new OrderItemsDto { Count = 1, EnName = orderItem.Notes }
                         : new OrderItemsDto { Count = 1, CnName = orderItem.Notes };
                 }
                 else
                     item = new OrderItemsDto { Count = 1, EnName = orderItem.Notes };
                
                 itemb.Add(item);
             }

             y = DrawItemLineThreeColsWrapped(fontFamily, paperWidth, img, y, textColor, $"{orderItem.Quantity}", itema,
                 $"${(orderItem.Price * orderItem.Quantity + itembMoney):0.00}", itemb);
         }

         return y;
     }

     private static bool IsGetLanguageValue(PrinterLanguageType? languageType, string language)
     {
         switch (languageType)
         {
             case null:
             case PrinterLanguageType.Chinese when language == "cn":
             case PrinterLanguageType.English when language == "en":
             case PrinterLanguageType.EnglishAndChinese:
                 return true;
             default:
                 return false;
         }
     }

     private static int DrawItemLine(int paperWidth, Image<Rgba32> img, int y, Color textColor, float lineHeight,
         string item, string price, Font? itemFont = null, FontStyle style = FontStyle.Regular, int size = 30, 
         FontFamily fontFamily = default)
     {
         itemFont ??= new Font(fontFamily, size, style);

         var priceSize = TextMeasurer.MeasureSize(price, new TextOptions(itemFont));
         var maxTextWidth = paperWidth - 20 - priceSize.Width;

         img.Mutate(ctx =>
         {
             ctx.DrawText(item, itemFont, textColor, new PointF(10, y));
             ctx.DrawText(price, itemFont, textColor, new PointF(maxTextWidth, y));
         });

         return y + (int)lineHeight + 10;
     }
     
     private static int DrawLine(int paperWidth, Image<Rgba32> img, int y, Color textColor, string text, Font font
         , float spacing = 20, bool rightAlign = false, bool centerAlign = false, int yOffset = 0)
     {
         var maxWidth = paperWidth - 20;
         var lines = new List<string>();
            
         var tokens = Regex.Matches(text, @"\w+|[^\w\s]|\s").Select(m => m.Value).ToList();

         var currentLine = "";
         foreach (var token in tokens)
         {
             var testLine = currentLine + token;
             var size = TextMeasurer.MeasureSize(testLine, new TextOptions(font));

             if (size.Width > maxWidth && !string.IsNullOrWhiteSpace(currentLine))
             {
                 if (Regex.IsMatch(token, @"[,.!?;:，。！？；：]") && lines.Count > 0)
                 {
                     lines[^1] += token;
                     currentLine = "";
                 }
                 else
                 {
                     lines.Add(currentLine);
                     currentLine = token.TrimStart();
                 }
             }
             else
             {
                 currentLine = testLine;
             }
         }

         if (!string.IsNullOrEmpty(currentLine))
             lines.Add(currentLine);
            
         Log.Information("lins:{@lines}", lines);
            
         foreach (var line in lines)
         {
             var size = TextMeasurer.MeasureSize(line, new TextOptions(font));
             float x = 10;

             if (centerAlign)
                 x = (paperWidth - size.Width) / 2;
             else if (rightAlign)
                 x = paperWidth - size.Width - 10;

             Log.Information("line:{@line}, size{@size}", line, size);
                
             img.Mutate(ctx => ctx.DrawText(line, font, textColor, new PointF(x, y + yOffset)));
                
             y += (int)size.Height + (int)spacing;
         }
         
         return y;
     }
     
     private static string GenerateFullLine(char fillChar, Font font, float maxWidth)
     {
         var charSize = TextMeasurer.MeasureSize(fillChar.ToString(), new TextOptions(font));
         var charWidth = charSize.Width;

         if (charWidth <= 0) 
             return "";
            
         var repeatCount = (int)(maxWidth / charWidth);

         var sb = new StringBuilder();
         for (var i = 0; i < repeatCount; i++)
         {
             sb.Append(fillChar);
         }
            
         while (TextMeasurer.MeasureSize(sb.ToString(), new TextOptions(font)).Width < maxWidth)
         {
             sb.Append(fillChar);
         }

         while (TextMeasurer.MeasureSize(sb.ToString(), new TextOptions(font)).Width > maxWidth && sb.Length > 0)
         {
             sb.Length--;
         }

         return sb.ToString();
     }
     
     private static int DrawSolidLine(int paperWidth, Image<Rgba32> img, int y, float thickness = 3, float spacing = 10, Color? color = null, float padding = 10)
     {
         color ??= Color.Black;

         var x1 = padding;
         var x2 = paperWidth - padding;

         img.Mutate(ctx =>
         {
             ctx.DrawLine(color.Value, thickness, new PointF(x1, y), new PointF(x2, y));
         });

         return y + (int)thickness + (int)spacing;
     }
}