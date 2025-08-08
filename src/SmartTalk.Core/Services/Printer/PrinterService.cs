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
using AutoMapper;
using Serilog;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.AliYun;
using SmartTalk.Messages.Commands.Printer;
using SmartTalk.Messages.Dto.Printer;
using SmartTalk.Messages.Enums.Printer;
using SmartTalk.Messages.Events.Printer;
using Color = SixLabors.ImageSharp.Color;

namespace SmartTalk.Core.Services.Printer;

public interface IPrinterService : IScopedDependency
{
    Task<GetPrinterJobAvailableResponse> GetPrinterJobAvailable(GetPrinterJobAvailableRequest request,
        CancellationToken cancellationToken);
    
    Task<string> UploadOrderPrintImageToQiNiuAndUpdatePrintUrlAsync(Guid jobToken, DateTimeOffset printDate,
        CancellationToken cancellationToken);
    
    Task<PrinterJobResponse> PrinterJob(PrinterJobCommand command, CancellationToken cancellationToken);
    
    Task<PrinterJobConfirmedEvent> ConfirmPrinterJob(ConfirmPrinterJobCommand command,
        CancellationToken cancellationToken);
    
    Task<PrinterJobConfirmedEvent> RecordPrintErrorAfterConfirmPrinterJob(ConfirmPrinterJobCommand command,
        CancellationToken cancellationToken);
}

public class PrinterService : IPrinterService
{
    private readonly IMapper _mapper;
    private readonly ICacheManager _cacheManager;
    private readonly IAliYunOssService _ossService;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IPrinterDataProvider _printerDataProvider;

    public PrinterService(IMapper mapper,ICacheManager cacheManager, IAliYunOssService ossService, IAgentDataProvider agentDataProvider, IPrinterDataProvider printerDataProvider)
    {
        _mapper = mapper;
        _cacheManager = cacheManager;
        _ossService = ossService;
        _agentDataProvider = agentDataProvider;
        _printerDataProvider = printerDataProvider;
    }

    public async Task<GetPrinterJobAvailableResponse> GetPrinterJobAvailable(GetPrinterJobAvailableRequest request, CancellationToken cancellationToken)
    {
        var key = $"{request.PrinterMac}-{request.Token}";
        if (await _cacheManager.GetAsync<object>(key, new MemoryCachingSetting(), cancellationToken).ConfigureAwait(false) == null)
        {
            return null;
        }

        var merchPrinter = await _printerDataProvider.GetMerchPrinterByPrinterMacAsync(request.PrinterMac, request.Token, cancellationToken).ConfigureAwait(false);

        if (merchPrinter == null)
        {
            await _cacheManager.SetAsync(key,1, new MemoryCachingSetting(TimeSpan.FromMinutes(2)),cancellationToken).ConfigureAwait(false);
            return null; 
        }
            
        var merchPrinterJob = await GetMerchPrinterJob(merchPrinter, cancellationToken);

        return new GetPrinterJobAvailableResponse()
        {
            JobReady = merchPrinterJob != null,
            JobToken = merchPrinterJob?.Id,
        };
    }
    
    private async Task<MerchPrinterOrder> GetMerchPrinterJob(MerchPrinter merchPrinter, CancellationToken cancellationToken)
    {
        var agent = await _agentDataProvider.GetAgentByIdAsync(merchPrinter.AgentId, cancellationToken);
        if (agent is null)
        {
            return null;
        }

        var now = DateTimeOffset.Now;
        
        if (!merchPrinter.IsEnabled)
        {
            return (await _printerDataProvider.GetMerchPrinterOrdersAsync(null, merchPrinter.AgentId,  PrintStatus.Waiting, now, merchPrinter.PrinterMac, true, cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        }
        else
        {
            var merchPrinterOrders = await  _printerDataProvider.GetMerchPrinterOrdersAsync(isOrderByPrintDate: true, cancellationToken: cancellationToken).ConfigureAwait(false);
            foreach (var merchPrinterOrder in merchPrinterOrders)
            {
                if (string.IsNullOrEmpty(merchPrinterOrder.PrinterMac) || merchPrinterOrder.PrinterMac == merchPrinter.PrinterMac)
                {
                    return merchPrinterOrder;
                }
            }
        }
        return null;
    }

    public async Task<string> UploadOrderPrintImageToQiNiuAndUpdatePrintUrlAsync(Guid jobToken, DateTimeOffset printDate,
        CancellationToken cancellationToken)
    {
        var merchPrinterOrder = (await _printerDataProvider.GetMerchPrinterOrdersAsync(jobToken, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        if (merchPrinterOrder == null) return string.Empty;
        if (!string.IsNullOrEmpty(merchPrinterOrder.ImageUrl))
        {
            return merchPrinterOrder.ImageUrl;
        }

        var img = await RenderReceipt().ConfigureAwait(false);
        var imageData = ConvertImageToEscPos(img);

        var imageKey = Guid.NewGuid().ToString();
        
        _ossService.UploadFile(imageKey, imageData);
        
        merchPrinterOrder.ImageKey = imageKey;
        merchPrinterOrder.ImageUrl = _ossService.GetFileUrl(imageKey);

        Log.Information(@"MerchPrinterOrder image key: {ImageKey}, url: {ImageUrl}", merchPrinterOrder.ImageKey,  merchPrinterOrder.ImageUrl);
        
        await _printerDataProvider.UpdateMerchPrinterOrderAsync(merchPrinterOrder, cancellationToken: cancellationToken).ConfigureAwait(false);

        return merchPrinterOrder.ImageUrl;
    }

    public async Task<PrinterJobResponse> PrinterJob(PrinterJobCommand command, CancellationToken cancellationToken)
    {
        var merchPrinterOrder = (await _printerDataProvider.GetMerchPrinterOrdersAsync(command.JobToken, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        if (merchPrinterOrder != null && merchPrinterOrder.PrintStatus == PrintStatus.Waiting)
        {
            merchPrinterOrder.PrintStatus = PrintStatus.Printing;
            await _printerDataProvider.UpdateMerchPrinterOrderAsync(merchPrinterOrder, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return new PrinterJobResponse()
        {
            MerchPrinterOrder = _mapper.Map<MerchPrinterOrderDto>(merchPrinterOrder)
        };
    }
    
    public async Task<PrinterJobConfirmedEvent> ConfirmPrinterJob(ConfirmPrinterJobCommand command,
        CancellationToken cancellationToken)
    {
        var merchPrinterOrder = (await _printerDataProvider.GetMerchPrinterOrdersAsync(
            jobToken: command.JobToken , cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        
        if (merchPrinterOrder != null && merchPrinterOrder.PrintStatus == PrintStatus.Printing)
        {
            merchPrinterOrder.PrintStatus = PrintStatus.Printed;
           
            await _printerDataProvider.UpdateMerchPrinterOrderAsync(merchPrinterOrder, cancellationToken: cancellationToken).ConfigureAwait(false);

            return new PrinterJobConfirmedEvent()
            {
                MerchPrinterOrderDto = _mapper.Map<MerchPrinterOrderDto>(merchPrinterOrder),
                PrinterMac = command.PrinterMac,
                PrinterStatusCode = command.PrintStatusCode
            };
        }

        return null;
    }
    
    public async Task<PrinterJobConfirmedEvent> RecordPrintErrorAfterConfirmPrinterJob(
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
            
            return new PrinterJobConfirmedEvent()
            {
                MerchPrinterOrderDto = _mapper.Map<MerchPrinterOrderDto>(merchPrinterOrder),
                PrinterMac = command.PrinterMac,
                PrinterStatusCode = command.PrintStatusCode
            };
        }

        return null;
    }

    private async Task<Image<Rgba32>> RenderReceipt()
    {
        int width = 512;
        Color textColor = Color.Black;
        Color bgColor = Color.White;

        string fontPath = "font/SourceHanSansSC-Regular.otf"; // 中文字体
        var collection = new FontCollection();
        var family = collection.Add(fontPath);
        collection.Add("font/SourceHanSansSC-Bold.otf");

        Font CreateFont(float size, bool bold = false)
        {
            return new Font(family, size, bold ? FontStyle.Bold : FontStyle.Regular);
        }
        
        var fontSmall = CreateFont(25);
        var fontMaxSmall = CreateFont(23);
        var fontNormal = CreateFont(30);
        var fontBold = CreateFont(40, bold: true);
        

        // 用“口”测量统一行高
        float lineHeight = TextMeasurer.MeasureSize("口", new TextOptions(fontNormal)).Height;

        var img = new Image<Rgba32>(width, 3000);
        img.Mutate(ctx => ctx.Fill(bgColor));

        int y = 10;

        void DrawLine(string text, Font font, float spacing = 20, bool rightAlign = false, bool centerAlign = false)
        {
            var size = TextMeasurer.MeasureSize(text, new TextOptions(font));
            float x = 10;

            if (centerAlign)
                x = (width - size.Width) / 2;
            else if (rightAlign)
                x = width - size.Width - 10;

            img.Mutate(ctx => ctx.DrawText(text, font, textColor, new PointF(x, y)));
            y += (int)lineHeight + (int)spacing;
        }
        
        string GenerateFullLine(char fillChar, Font font)
        {
            string line = new string(fillChar, 1);
            float lineWidth = TextMeasurer.MeasureSize(line, new TextOptions(font)).Width;

            if (lineWidth == 0) return "";

            int repeatCount = (int)Math.Ceiling(width / lineWidth);
            return new string(fillChar, repeatCount);
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
        
        string InsertLineBreakBetweenEnglishAndChinese(string text)
        {
            var sb = new StringBuilder();
            int i = 0;

            while (i < text.Length)
            {
                char current = text[i];
                CharType currType = GetCharType(current);
     
                // 当前字符先写入
                sb.Append(current);
                
                int j = i + 1;
                while (j < text.Length && GetCharType(text[j]) == CharType.Other)
                {
                    sb.Append(text[j]);
                    i = j;
                    j++;
                }

                if (j < text.Length)
                {
                    CharType nextType = GetCharType(text[j]);

                    // 若中英混合，插入换行
                    if (currType != CharType.Other && nextType != CharType.Other && currType != nextType)
                    {
                        sb.Append('\n');
                    }
                }

                i++;
            }

            return sb.ToString();
        }
        
        CharType GetCharType(char c)
        {
            if (IsEnglish(c)) return CharType.English;
            if (IsChinese(c)) return CharType.Chinese;
            return CharType.Other;
        }
        
        bool IsChinese(char c) =>
            (c >= 0x4E00 && c <= 0x9FFF) ||
            (c >= 0x3400 && c <= 0x4DBF) ||
            (c >= 0x3000 && c <= 0x303F);

        bool IsEnglish(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        
        void DrawItemLineThreeColsWrapped(string qty, string itemName, string price, Dictionary<string, int>? remarks = null,
        float fontSize = 27, bool bold = false, float lineSpacing = 1.5f, float remarkLineSpacing = 1.5f, int backSpacing = 0)
        {
            var font = new Font(family, fontSize, bold ? FontStyle.Bold : FontStyle.Regular);
            var boldFont = new Font(family, fontSize, FontStyle.Bold);
            var remarkFont = new Font(family, fontSize - 3, bold ? FontStyle.Regular : FontStyle.Regular);
            
            float padding = 10;
            float col1Width = 80;
            float col3Width = 150;
            float col2Width = width - col1Width - col3Width - padding * 2;

            float col1X = padding;
            float col2X = col1X + col1Width;
            float col3X = width - col3Width - padding;

            var baseOptions = new TextOptions(font)
            {
                WrappingLength = col2Width,
                LineSpacing = lineSpacing,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            // 缩进计算
            float indentX = 0;
            if (!string.IsNullOrEmpty(itemName))
            {
                var firstChar = itemName[0].ToString();
                indentX = TextMeasurer.MeasureSize(firstChar, baseOptions).Width;
            }

            // 应用中英文换行
            itemName = InsertLineBreakBetweenEnglishAndChinese(itemName);

            // 测量 itemName 高度
            float itemBlockHeight = TextMeasurer.MeasureSize(itemName, baseOptions).Height;

            // ======= 备注处理 ========
            List<(string prefix, string content)> remarkLines = new();
            if (remarks != null)
            {
                foreach (var kvp in remarks)
                {
                    var raw = kvp.Key.Trim();
                    var qtyVal = kvp.Value;
                    string prefix = qtyVal > 0 ? $"{qtyVal}" : ">";
                    string content = InsertLineBreakBetweenEnglishAndChinese(raw);
                    remarkLines.Add((prefix, content));
                }
            }

            float remarkExtraWidth = 50; // 备注区域宽于 itemName
            var remarkOptions = new TextOptions(remarkFont)
            {
                WrappingLength = col2Width + remarkExtraWidth - indentX,
                LineSpacing = remarkLineSpacing,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            float remarkBlockHeight = 0f;
            foreach (var (_, content) in remarkLines)
            {
                remarkBlockHeight += TextMeasurer.MeasureSize(content, remarkOptions).Height + 5;
            }

            float totalBlockHeight = itemBlockHeight + (remarkBlockHeight > 0 ? remarkBlockHeight : 0);

            // ======= 开始绘制 ========
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
                float priceX = col3X + col3Width - priceSize.Width;
                
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

                // 绘制备注内容
                float remarkY = y + itemBlockHeight + 5;
                foreach (var (prefix, content) in remarkLines)
                {
                    var prefixOptions = new RichTextOptions(remarkFont)
                    {
                        Origin = new PointF(col2X + indentX, remarkY),
                        WrappingLength = col2Width + remarkExtraWidth - indentX,
                        LineSpacing = remarkLineSpacing
                    };
                    ctx.DrawText(prefixOptions, prefix, textColor);

                    float prefixWidth = TextMeasurer.MeasureSize(prefix, new TextOptions(font)).Width;
                    float spaceWidth = TextMeasurer.MeasureSize(" ", new TextOptions(font)).Width;
                    float contentX = col2X + indentX + prefixWidth + spaceWidth * 4;

                    var contentOptions = new RichTextOptions(remarkFont)
                    {
                        Origin = new PointF(contentX, remarkY),
                        WrappingLength = col2Width + remarkExtraWidth - (contentX - col2X),
                        LineSpacing = remarkLineSpacing
                    };
                    ctx.DrawText(contentOptions, content.TrimStart(), textColor);

                    float lineHeight = TextMeasurer.MeasureSize(content, remarkOptions).Height + 5;
                    remarkY += lineHeight;
                }
            });

            y += (int)totalBlockHeight + 12 + backSpacing;
        }

        
        //实线
        void DrawSolidLine(float thickness = 2, float spacing = 10, Color? color = null, float padding = 10)
        {
            color ??= Color.Black;

            float x1 = padding;
            float x2 = width - padding;

            img.Mutate(ctx =>
            {
                ctx.DrawLine(color.Value, thickness, new PointF(x1, y), new PointF(x2, y));
            });

            y += (int)thickness + (int)spacing;
        }
        
        //虚线
        void DrawDashedLine() => DrawLine(GenerateFullLine('-', fontNormal), fontNormal);
        
        void DrawDashedBoldLine() => DrawLine(GenerateFullLine('-', fontBold), fontBold);


        //===== 开始绘制内容 =====

        DrawLine("#44", fontNormal);
        DrawLine("Call-in Order",  CreateFont(45, true), spacing: 50, centerAlign: true);
        DrawLine("chongqing hot", fontMaxSmall, centerAlign: true);
        DrawLine("21385 S Western Ave, Torrance, CA 90501,USA", fontMaxSmall);
        DrawLine("(902)-316-2148", fontMaxSmall, centerAlign: true);
        
        DrawDashedBoldLine();
        
        DrawItemLine("07/05/2025 12:14 AM", "Delivery", fontSmall);
        DrawItemLine("shouju", "AiPhoneOrder", fontSmall);
        
        DrawSolidLine();
        
        DrawLine("新的name", fontSmall);
        DrawLine("(902)-316-2140", fontSmall);
        DrawLine("625 Vista Way, Milpitas, CA", fontSmall);
        
        DrawSolidLine();
        
        DrawLine("Order Remark:订单备注", fontNormal);
        
        DrawDashedLine();
        
        DrawItemLineThreeColsWrapped("QTY", "Items", "Total", fontSize: 25, bold: true, backSpacing: 15);
        DrawItemLineThreeColsWrapped("1", "Pan Fried Spaghetti w. Shredded Beef in Black Pepper Sauce 黑椒牛柳炒意粉", "$15.00");
        DrawItemLineThreeColsWrapped("1", "Spicy Beef Tripe & Tendon Noodle in Soup 香辣牛肚牛筋汤面", "$10.00",new Dictionary<string, int>{{"Kumquat Tea($15.00) 金桔茶", 0}, {"Kumquat Tea($15.00) 金桔茶2", 2}});
        DrawItemLineThreeColsWrapped("1", "Kumquat Tea 金桔茶", "$45.00", new Dictionary<string, int>{{"Kumquat($15.00) 金桔茶", 0}});
        
        DrawDashedLine();
        
        DrawItemLine("Subtotal", "$70.00", fontSmall);
        DrawItemLine("Tax", "$12.00", fontSmall);
        DrawItemLine("Total", "$82.00", fontNormal);
        
        DrawDashedLine();
        
        DrawLine("*** Unpaid ***", CreateFont(35, true), spacing: 15, centerAlign: true);
        
        DrawDashedLine();
        
        DrawLine("", fontNormal, centerAlign: true);
        DrawLine("", fontNormal, centerAlign: true);
        DrawLine("Print Time 07/09/2025 11:48 PM", fontSmall, centerAlign: true);
        DrawLine("Powered by SmartTalk AI", fontSmall, centerAlign: true);

        img.Mutate(x => x.Crop(new Rectangle(0, 0, width, y + 10)));
        return img;
    }

    private byte[] ConvertImageToEscPos(Image<Rgba32> image)
    {
        // 转成单色黑白位图
        var threshold = 140;
        int width = image.Width;
        int height = image.Height;
        int bytesPerRow = (width + 7) / 8;

        byte[] escpos = new byte[height * bytesPerRow + 8 * height]; // 预留充足
        using var ms = new MemoryStream();

        for (int y = 0; y < height; y++)
        {
            ms.WriteByte(0x1D); // GS v 0
            ms.WriteByte(0x76);
            ms.WriteByte(0x30);
            ms.WriteByte(0x00); // Normal mode

            ms.WriteByte((byte)(bytesPerRow % 256)); // xL
            ms.WriteByte((byte)(bytesPerRow / 256)); // xH
            ms.WriteByte(0x01);                      // yL (1行)
            ms.WriteByte(0x00);                      // yH

            for (int xByte = 0; xByte < bytesPerRow; xByte++)
            {
                byte b = 0x00;
                for (int bit = 0; bit < 8; bit++)
                {
                    int x = xByte * 8 + bit;
                    if (x >= width) continue;

                    var pixel = image[x, y];
                    var luminance = (0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);

                    if (luminance < threshold)
                        b |= (byte)(1 << (7 - bit));
                }
                ms.WriteByte(b);
            }
        }

        return ms.ToArray();
    }
}