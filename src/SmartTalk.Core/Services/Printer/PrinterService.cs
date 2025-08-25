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
using Aliyun.OSS;
using AutoMapper;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.AliYun;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Message.Commands.Printer;
using SmartTalk.Message.Events.Printer;
using SmartTalk.Messages.Commands.Printer;
using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Dto.Printer;
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
    
    Task PrintTestAsync(PrintTestCommand command, CancellationToken cancellationToken);

    Task PrinterStatusChangedAsync(PrinterStatusChangedEvent @event, CancellationToken cancellationToken);

    Task PrinterJobConfirmeAsync(PrinterJobConfirmedEvent @event, CancellationToken cancellationToken);
    
    Task AddMerchPrinterAsync(AddMerchPrinterCommand command, CancellationToken cancellationToken);
    
    Task<GetMerchPrintersResponse> GetMerchPrintersAsync(GetMerchPrintersRequest request, CancellationToken cancellationToken);
    
    Task DeleteMerchPrinterAsync(DeleteMerchPrinterCommand command, CancellationToken cancellationToken);
    
    Task UpdateMerchPrinterAsync(UpdateMerchPrinterCommand command, CancellationToken cancellationToken);
    
    Task<GetMerchPrinterLogResponse> GetMerchPrinterLog(GetMerchPrinterLogRequest request, CancellationToken cancellationToken);
}

public class PrinterService : IPrinterService
{
    private readonly IMapper _mapper;
    private readonly ICacheManager _cacheManager;
    private readonly IAliYunOssService _ossService;
    private readonly IPosDataProvider _posDataProvider;
    private readonly IPrinterDataProvider _printerDataProvider;

    public PrinterService(IMapper mapper,ICacheManager cacheManager, IAliYunOssService ossService, IPosDataProvider posDataProvider, IPrinterDataProvider printerDataProvider)
    {
        _mapper = mapper;
        _cacheManager = cacheManager;
        _ossService = ossService;
        _posDataProvider = posDataProvider;
        _printerDataProvider = printerDataProvider;
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
        
        var img = await RenderReceiptAsync(order.OrderNo,  
            JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(store.Names).GetValueOrDefault("en")?.GetValueOrDefault("name"),
            store.Address,
            store.PhoneNums,
            order.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss"),
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
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).ConfigureAwait(false);
       
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

    public async Task PrintTestAsync(PrintTestCommand command, CancellationToken cancellationToken)
    {
        var merchPrinterOrder = new MerchPrinterOrder
        {
            OrderId = 0,
            StoreId = command.StoreId,
            PrinterMac = command.PrinterMac,
            PrintDate = DateTimeOffset.Now
        };
        
        await _printerDataProvider.AddMerchPrinterOrderAsync(merchPrinterOrder, cancellationToken).ConfigureAwait(false);
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
            
        await _printerDataProvider.AddMerchPrinterLogAsync(merchPrinterLog, cancellationToken);
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
            float maxWidth = width - 20;
            List<string> lines = new List<string>();
            
            string currentLine = "";
            foreach (char c in text)
            {
                string testLine = currentLine + c;
                var size = TextMeasurer.MeasureSize(testLine, new TextOptions(font));

                if (size.Width > maxWidth && currentLine.Length > 0)
                {
                    lines.Add(currentLine);
                    currentLine = c.ToString();
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

                img.Mutate(ctx => ctx.DrawText(line, font, textColor, new PointF(x, y)));
                y += (int)size.Height + (int)spacing;
            }
        }
        
        string GenerateFullLine(char fillChar, Font font, float maxWidth)
        {
            var charSize = TextMeasurer.MeasureSize(fillChar.ToString(), new TextOptions(font));
            float charWidth = charSize.Width;

            if (charWidth <= 0) 
                return "";
            
            int repeatCount = (int)(maxWidth / charWidth);

            var sb = new StringBuilder();
            for (int i = 0; i < repeatCount; i++)
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

        string InsertLineBreakBetweenEnglishAndChinese(string text, Font font, float maxWidth)
        {
            var sb = new StringBuilder();
            var lineBuffer = new StringBuilder();

            int i = 0;
            while (i < text.Length)
            {
                var current = text[i];
                var currType = GetCharType(current);
                lineBuffer.Append(current);
                
                var size = TextMeasurer.MeasureSize(lineBuffer.ToString(), new TextOptions(font));
                if (size.Width > maxWidth && lineBuffer.Length > 1)
                {
                    sb.AppendLine(lineBuffer.ToString(0, lineBuffer.Length - 1));
                    lineBuffer.Clear();
                    lineBuffer.Append(current);
                }

                var j = i + 1;
                while (j < text.Length && GetCharType(text[j]) == CharType.Other)
                {
                    lineBuffer.Append(text[j]);
                    i = j;
                    j++;
                }

                if (j < text.Length)
                {
                    var nextType = GetCharType(text[j]);
                    if (currType != CharType.Other && nextType != CharType.Other && currType != nextType)
                    {
                        sb.AppendLine(lineBuffer.ToString());
                        lineBuffer.Clear();
                    }
                }

                i++;
            }

            if (lineBuffer.Length > 0)
            {
                sb.Append(lineBuffer.ToString());
            }

            return sb.ToString();
        }
        
        CharType GetCharType(char c)
        {
            if (IsEnglish(c)) return CharType.English;
            if (IsChinese(c)) return CharType.Chinese;
            
            return CharType.Other;
        }
        
        bool IsChinese(char c) => (c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DBF) || (c >= 0x3000 && c <= 0x303F);

        bool IsEnglish(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        
        void DrawItemLineThreeColsWrapped(string qty, string itemName, string price, Dictionary<string, int>? remarks = null,
        float fontSize = 27, bool bold = false, float lineSpacing = 1.5f, float remarkLineSpacing = 1.5f, int backSpacing = 0)
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
            
            if (!string.IsNullOrEmpty(itemName))
            {
                var firstChar = itemName[0].ToString();
                indentX = TextMeasurer.MeasureSize(firstChar, baseOptions).Width;
            }
            
            itemName = InsertLineBreakBetweenEnglishAndChinese(itemName, font, col2Width);
            
            var itemBlockHeight = TextMeasurer.MeasureSize(itemName, baseOptions).Height;
            
            List<(string prefix, string content)> remarkLines = [];
            
            Log.Information("Remarks:{@remarks}", remarks);
            
            var remarkExtraWidth = 50;
            
            if (remarks != null)
            {
                foreach (var kvp in remarks)
                {
                    var raw = kvp.Key.Trim();
                    var qtyVal = kvp.Value;
                    var prefix = qtyVal > 1 ? $"{qtyVal}" : ">";
                    var content = InsertLineBreakBetweenEnglishAndChinese(raw, remarkFont,col2Width + remarkExtraWidth - indentX);
                    remarkLines.Add((prefix, content));
                }
            }
            
            var remarkOptions = new TextOptions(remarkFont)
            {
                WrappingLength = col2Width + remarkExtraWidth - indentX,
                LineSpacing = remarkLineSpacing,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            var remarkBlockHeight = 0f;
            
            foreach (var (_, content) in remarkLines)
            {
                remarkBlockHeight += TextMeasurer.MeasureSize(content, remarkOptions).Height + 5;
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
                
                ctx.DrawText(itemTextOptions, itemName, textColor);
                
                var remarkY = y + itemBlockHeight + 5;
                foreach (var (prefix, content) in remarkLines)
                {
                    var prefixOptions = new RichTextOptions(remarkFont)
                    {
                        Origin = new PointF(col2X + indentX, remarkY),
                        WrappingLength = col2Width + remarkExtraWidth - indentX,
                        LineSpacing = remarkLineSpacing
                    };
                    ctx.DrawText(prefixOptions, prefix, textColor);

                    var prefixWidth = TextMeasurer.MeasureSize(prefix, new TextOptions(font)).Width;
                    var spaceWidth = TextMeasurer.MeasureSize(" ", new TextOptions(font)).Width;
                    var contentX = col2X + indentX + prefixWidth + spaceWidth * 4;

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

        
        void DrawSolidLine(float thickness = 2, float spacing = 10, Color? color = null, float padding = 10)
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
        
        void DrawDashedLine() => DrawLine(GenerateFullLine('-', fontNormal, width), fontNormal);
        
        void DrawDashedBoldLine() => DrawLine(GenerateFullLine('-', fontBold, width), fontBold);

        Log.Information("orderItems: {@orderItems}", orderItems);
        
        DrawLine($"#{printNumber}", fontNormal);
        DrawLine($"{orderType} Order",  CreateFont(45, true), spacing: 50, centerAlign: true);
        DrawLine($"{restaurantName}", fontMaxSmall, centerAlign: true);
        
        if (!string.IsNullOrEmpty(restaurantAddress))
            DrawLine($"{restaurantAddress}", fontMaxSmall, centerAlign: true);
        
        DrawLine($"{restaurantPhone}", fontMaxSmall, centerAlign: true);
        
        DrawDashedBoldLine();
        
        DrawItemLine($"{orderTime}", $"{orderType}", fontSmall);
        DrawItemLine($"{printerName}", "AiPhoneOrder", fontSmall);
        
        DrawSolidLine();

        if (!string.IsNullOrEmpty(guestName))
            DrawLine($"{guestName}", fontSmall);

        if (!string.IsNullOrEmpty(guestPhone))
            DrawLine($"{guestPhone}", fontSmall);
        
        if (!string.IsNullOrEmpty(guestAddress))
            DrawLine($"{guestAddress}", fontSmall);

        if (!string.IsNullOrEmpty(orderNotes))
        {
            DrawSolidLine();
            DrawLine($"Order Remark:{orderNotes}", fontNormal);
        }
        
        DrawDashedLine();
        
        DrawItemLineThreeColsWrapped("QTY", "Items", "Total", fontSize: 25, bold: true, backSpacing: 15);
       
        foreach (var orderItem in orderItems)
        {
            var res = orderItem.OrderItemModifiers.Select(x => (
                $"{x.ModifierLocalizations.FirstOrDefault(s => s.Field == "name" && s.LanguageCode == "en_US")?.Value} (${x.Price})n {x.ModifierLocalizations.FirstOrDefault(s => s.Field == "name" && s.LanguageCode == "zh_CN")?.Value}",
                orderItem.Quantity)).ToDictionary(x => x.Item1, x => x.Item2);
            
            if (!string.IsNullOrEmpty(orderItem.Notes))
            {
                res.Add($"{orderItem.Notes}", 0);   
            }
            
            DrawItemLineThreeColsWrapped($"{orderItem.Quantity}", $"{orderItem.ProductNames.GetValueOrDefault("en")?.GetValueOrDefault("posName")} {orderItem.ProductNames.GetValueOrDefault("cn")?.GetValueOrDefault("posName")}", $"${orderItem.Price * orderItem.Quantity}", res);
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
            Result = result
        };
    }
    
    public async Task AddMerchPrinterAsync(AddMerchPrinterCommand command, CancellationToken cancellationToken)
    {
        var printer = (await _printerDataProvider.GetMerchPrintersAsync(printerMac: command.PrinterMac, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        if (printer != null)
        {
            throw new Exception($"The printer [{command.PrinterMac}] already exists");
        }

        printer = _mapper.Map<MerchPrinter>(command);
        printer.Token = await GetOrCreatePrinterTokenAsync(command.PrinterMac, cancellationToken).ConfigureAwait(false);

        await CheckPrinterCanOnlyHaveOneEnabled(printer, cancellationToken);

        await _printerDataProvider.AddMerchPrinterAsync(printer, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteMerchPrinterAsync(DeleteMerchPrinterCommand command, CancellationToken cancellationToken)
    {
        var printer = (await _printerDataProvider.GetMerchPrintersAsync(id: command.Id, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        
       await _printerDataProvider.DeleteMerchPrinterAsync(printer, cancellationToken: cancellationToken).ConfigureAwait(false);
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

    public async Task UpdateMerchPrinterAsync(UpdateMerchPrinterCommand command, CancellationToken cancellationToken)
    {
        var printer = (await _printerDataProvider.GetMerchPrintersAsync(id: command.Id, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        _mapper.Map(command, printer);

        await CheckPrinterCanOnlyHaveOneEnabled(printer, cancellationToken).ConfigureAwait(false);
        
        await _printerDataProvider.UpdateMerchPrinterMacAsync(printer, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    
     public async Task<GetMerchPrinterLogResponse> GetMerchPrinterLog(GetMerchPrinterLogRequest request, CancellationToken cancellationToken)
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