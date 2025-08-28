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
            storePrintDate).ConfigureAwait(false);
       
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
        var merchPrinterLog = await GenerateMerchPrinterLogAsync(@event, cancellationToken).ConfigureAwait(false);
        
        if (merchPrinterLog != null)
            await _printerDataProvider.AddMerchPrinterLogAsync(merchPrinterLog, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MerchPrinterLog> GenerateMerchPrinterLogAsync(PrinterStatusChangedEvent @event, CancellationToken cancellationToken)
    {
        var varianceList = GetVarianceList(@event);
        
        if (!varianceList.Any()) return null;

        var merchPrinter = (await _printerDataProvider.GetMerchPrintersAsync(@event.PrinterMac, @event.Token, cancellationToken: cancellationToken)).FirstOrDefault();
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
            
        await _printerDataProvider.AddMerchPrinterLogAsync(merchPrinterLog, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Image<Rgba32>> RenderReceiptAsync(string printNumber, string restaurantName, string restaurantAddress,
        string restaurantPhone, string orderTime, string printerName, string orderType, string guestName, 
        string guestPhone, string guestAddress, string orderNotes, List<PhoneCallOrderItem> orderItems,
        string subtotal, string tax, string total, string printTime)
    {
        var width = 512;
        var textColor = Color.Black;
        var bgColor = Color.White;
        
        var regularFont = "/app/fonts/SourceHanSansSC-Regular.otf";
        var boldFont = "/app/fonts/SourceHanSansSC-Bold.otf";
        
        var collection = new FontCollection();
        var family = collection.Add(regularFont);
        collection.Add(boldFont);
        
        Font CreateFont(float size, bool bold = false)
        {
            return new Font(family, size, bold ? FontStyle.Bold : FontStyle.Regular);
        }
        
        var fontSmall = CreateFont(25);
        var fontMaxSmall = CreateFont(23);
        var fontNormal = CreateFont(30);
        var fontBold = CreateFont(40, bold: true);
        
        var lineHeight = TextMeasurer.MeasureSize("口", new TextOptions(fontNormal)).Height;

        var img = new Image<Rgba32>(width, 3000);
        img.Mutate(ctx => ctx.Fill(bgColor));

        var y = 10;

        void DrawLine(string text, Font font, float spacing = 20, bool rightAlign = false, bool centerAlign = false)
        {
            var maxWidth = width - 20;
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
            
            foreach (var line in lines)
            {
                var size = TextMeasurer.MeasureSize(line, new TextOptions(font));
                float x = 10;

                if (centerAlign)
                    x = (width - size.Width) / 2;
                else if (rightAlign)
                    x = width - size.Width - 10;
                
                var totalHeight = (int)spacing / 2;
                
                img.Mutate(ctx => ctx.DrawText(line, font, textColor, new PointF(x, y + totalHeight)));
                y += (int)size.Height + (int)spacing;
            }
        }
        
        string GenerateFullLine(char fillChar, Font font, float maxWidth)
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

        void DrawItemLine(string item, string price, Font? itemFont = null)
        {
            itemFont ??= fontNormal;
            var priceSize = TextMeasurer.MeasureSize(price, new TextOptions(itemFont));
            var maxTextWidth = width - 20 - priceSize.Width;

            img.Mutate(ctx =>
            {
                ctx.DrawText(item, itemFont, textColor, new PointF(10, y));
                ctx.DrawText(price, itemFont, textColor, new PointF(maxTextWidth, y));
            });

            y += (int)lineHeight + 10;
        }
        
        void DrawItemLineThreeColsWrapped(string qty, OrderItemsDto itemName, string price, List<OrderItemsDto> remarks = null,
        float fontSize = 27, bool bold = false, float lineSpacing = 1.6f, float remarkLineSpacing = 1.6f, int backSpacing = 10)
        {
            var font = new Font(family, fontSize, bold ? FontStyle.Bold : FontStyle.Regular);
            var boldFont = new Font(family, fontSize, FontStyle.Bold);
            var remarkFont = new Font(family, fontSize - 3, bold ? FontStyle.Regular : FontStyle.Regular);
            
            var padding = 10;
            var col1Width = 80;
            var col3Width = 150;
            var col2Width = width - col1Width - col3Width - padding * 2;

            var col1X = padding;
            var col2X = col1X + col1Width;
            var col3X = width - col3Width - padding;

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
            var towIndentx = TextMeasurer.MeasureSize("口口",baseOptions).Width;
            
            if (itemName != null)
            {
                firstChar = itemName.EnName;
                
                if (!string.IsNullOrEmpty(itemName.CnName))
                {
                    firstChar += "\n" + itemName.CnName;
                    
                    var lines = firstChar.Split('\n');
                
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        itemBlockHeight += TextMeasurer.MeasureSize(line, baseOptions).Height + 5;
                    }
                }
                else
                    itemBlockHeight = TextMeasurer.MeasureSize(firstChar, baseOptions).Height + 5;

                indentX = TextMeasurer.MeasureSize(firstChar, baseOptions).Width;
            }
            
            List<(string prefix, string content)> remarkLines = [];
            
            Log.Information("Remarks:{@remarks}", remarks);
            
            var remarkExtraWidth = 50;
            
            if (remarks != null)
            {
                foreach (var remark in remarks)
                {
                    var prefix = remark.Count > 1 ? $"{remark.Count}" : ">";
                    var content = remark.EnName + "\n" + remark.CnName;
                    remarkLines.Add((prefix, content));
                }
            }
            
            var remarkOptions = new TextOptions(remarkFont)
            {
                WrappingLength = col2Width + remarkExtraWidth - towIndentx,
                LineSpacing = remarkLineSpacing,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            var remarkBlockHeight = 0f;
            
            foreach (var (_, content) in remarkLines)
            {
                var lines = content.Split('\n');
                
                Log.Information("lines:{@lines}", lines);
                
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    remarkBlockHeight += TextMeasurer.MeasureSize(line, remarkOptions).Height;
                }
                
                remarkBlockHeight += 5;
            }

            var totalBlockHeight = itemBlockHeight + (remarkBlockHeight > 0 ? remarkBlockHeight : 0);
            
            img.Mutate(ctx =>
            {
                var qtyTextOptions = new RichTextOptions(boldFont)
                {
                    Origin = new PointF(col1X, y),
                    WrappingLength = col2Width,
                    LineSpacing = lineSpacing
                };
                ctx.DrawText(qtyTextOptions, qty, textColor);
                
                var priceSize = TextMeasurer.MeasureSize(price, new TextOptions(font));
                var priceX = col3X + col3Width - priceSize.Width;
                
                var priceTextOptions = new RichTextOptions(boldFont)
                {
                    Origin = new PointF(priceX, y),
                    WrappingLength = col2Width,
                    LineSpacing = lineSpacing
                };
                
                ctx.DrawText(priceTextOptions ,price, textColor);

                var itemTextOptions = new RichTextOptions(font)
                {
                    Origin = new PointF(col2X, y),
                    WrappingLength = col2Width,
                    LineSpacing = lineSpacing
                };
                
                ctx.DrawText(itemTextOptions, firstChar, textColor);
                
                var remarkY = y + itemBlockHeight + 15;
                foreach (var (prefix, content) in remarkLines)
                {
                    var prefixOptions = new RichTextOptions(remarkFont)
                    {
                        Origin = new PointF(col2X + towIndentx, remarkY),
                        WrappingLength = col2Width + remarkExtraWidth - indentX,
                        LineSpacing = remarkLineSpacing
                    };
                    ctx.DrawText(prefixOptions, prefix, textColor);

                    var prefixWidth = TextMeasurer.MeasureSize(prefix, new TextOptions(font)).Width;
                    var spaceWidth = TextMeasurer.MeasureSize(" ", new TextOptions(font)).Width;
                    var contentX = col2X + towIndentx + prefixWidth + spaceWidth * 4;

                    var contentOptions = new RichTextOptions(remarkFont)
                    {
                        Origin = new PointF(contentX, remarkY),
                        WrappingLength = col2Width + remarkExtraWidth - (contentX - col2X),
                        LineSpacing = remarkLineSpacing
                    };
                    ctx.DrawText(contentOptions, content.TrimStart(), textColor);

                    var lineHeight = TextMeasurer.MeasureSize(content, remarkOptions).Height + 5;
                    remarkY += lineHeight;
                }
            });

            y += (int)totalBlockHeight + 12 + backSpacing;
        }

        
        void DrawSolidLine(float thickness = 3, float spacing = 10, Color? color = null, float padding = 10)
        {
            color ??= Color.Black;

            var x1 = padding;
            var x2 = width - padding;

            img.Mutate(ctx =>
            {
                ctx.DrawLine(color.Value, thickness, new PointF(x1, y), new PointF(x2, y));
            });

            y += (int)thickness + (int)spacing;
        }
        
        void DrawDashedLine() => DrawLine(GenerateFullLine('-', fontNormal, width-20), fontNormal);
        
        void DrawDashedBoldLine() => DrawLine(GenerateFullLine('-', fontBold, width-20), fontBold, 20);

        Log.Information("orderItems: {@orderItems}", orderItems);
        
        DrawLine($"#{printNumber}", fontNormal);
        DrawLine($"{orderType} Order",  CreateFont(45, true), spacing: 50, centerAlign: true);
        DrawLine($"{restaurantName}", fontMaxSmall, centerAlign: true);
        
        if (!string.IsNullOrEmpty(restaurantAddress))
            DrawLine($"{restaurantAddress}", fontMaxSmall, centerAlign: true);

        if (!string.IsNullOrEmpty(restaurantPhone))
        {
            var phoneSplit = restaurantPhone.Split(",");

            var phones = "";
            
            foreach (var phone in phoneSplit)
            {
                phones += $"({phone[..3]})-{phone.Substring(3, 3)}-{phone[6..]},";
            }
            
            phones = phones.TrimEnd(',');
            
            DrawLine($"{phones}", fontMaxSmall, centerAlign: true);    
        }
        
        DrawDashedBoldLine();
        
        DrawItemLine($"{orderTime}", $"{orderType}", fontSmall);
        DrawItemLine($"{printerName}", "AiPhoneOrder", fontSmall);
        
        DrawSolidLine();

        if (!string.IsNullOrEmpty(guestName))
            DrawLine($"{guestName}", fontSmall);

        if (!string.IsNullOrEmpty(guestPhone))
        {
            var formatted = $"({guestPhone[..3]})-{guestPhone.Substring(3, 3)}-{guestPhone[6..]}";
            DrawLine($"{formatted}", fontSmall);    
        }
        
        if (!string.IsNullOrEmpty(guestAddress))
            DrawLine($"{guestAddress}", fontSmall);

        if (!string.IsNullOrEmpty(orderNotes))
        {
            DrawSolidLine();
            DrawLine($"Order Remark:{orderNotes}", fontNormal);
        }
        
        DrawDashedLine();
        
        DrawItemLineThreeColsWrapped("QTY", new OrderItemsDto{EnName = "Items"}, "Total", fontSize: 25, bold: true, backSpacing: 15);
       
        foreach (var orderItem in orderItems)
        {
            var itema = new OrderItemsDto()
            {
                EnName = orderItem.ProductNames.GetValueOrDefault("en")?.GetValueOrDefault("posName"),
                CnName = orderItem.ProductNames.GetValueOrDefault("cn")?.GetValueOrDefault("posName")
            };

            var itemb = orderItem.OrderItemModifiers.Select(x =>
            {
                return new OrderItemsDto()
                {
                    Count = orderItem.Quantity,
                    EnName = x.ModifierLocalizations.FirstOrDefault(s => s.Field == "name" && s.LanguageCode == "en_US")?.Value,
                    CnName = x.ModifierLocalizations.FirstOrDefault(s => s.Field == "name" && s.LanguageCode == "zh_CN")?.Value
                };
            }).ToList();
            
            if (!string.IsNullOrEmpty(orderItem.Notes))
            {
                itemb.Add(new OrderItemsDto
                {
                    Count = 1,
                    EnName = orderItem.Notes
                });   
            }
            
            DrawItemLineThreeColsWrapped($"{orderItem.Quantity}", itema, $"${orderItem.Price * orderItem.Quantity}", itemb);
        }
        
        DrawDashedLine();
        
        DrawItemLine("Subtotal", $"${subtotal}", fontSmall);
        DrawItemLine("Tax", $"${tax}", fontSmall);
        DrawItemLine("Total", $"${total}", fontNormal);
        
        DrawDashedLine();
        
        DrawLine("*** Unpaid ***", CreateFont(35, true), spacing: 15, centerAlign: true);
        
        DrawDashedLine();
        
        DrawLine("", fontNormal, centerAlign: true);
        DrawLine("", fontNormal, centerAlign: true);
        DrawLine($"Print Time {printTime}", fontSmall, centerAlign: true);
        DrawLine($"Powered by SmartTalk AI", fontSmall, centerAlign: true);

        img.Mutate(x => x.Crop(new Rectangle(0, 0, width, y + 10)));
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
}