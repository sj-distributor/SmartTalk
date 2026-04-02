namespace SmartTalk.Core.Services.AiSpeechAssistant;

using global::System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using SmartTalk.Core.Ioc;
using UglyToad.PdfPig;

public interface IFileTextExtractor : IScopedDependency
{
    Task<string> ExtractAsync(string fileUrl, CancellationToken cancellationToken);

    Task<string> ExtractAsync(string fileUrl, string fileName, CancellationToken cancellationToken);
}

public class FileTextExtractor : IFileTextExtractor
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".csv", ".html", ".htm", ".json", ".xml"
    };

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".csv", ".html", ".htm", ".json", ".xml",
        ".pdf", ".docx", ".xlsx", ".xls"
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public FileTextExtractor(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> ExtractAsync(string fileUrl, CancellationToken cancellationToken)
    {
        return await ExtractAsync(fileUrl, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ExtractAsync(string fileUrl, string fileName, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient();
        using var response = await client.GetAsync(fileUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes == null || bytes.Length == 0)
            return string.Empty;

        var extension = ResolveExtension(fileUrl, fileName, response.Content?.Headers?.ContentType?.MediaType);
        if (!string.IsNullOrWhiteSpace(extension))
            return ExtractByExtension(extension, bytes);

        throw new NotSupportedException($"Unsupported file URL: '{fileUrl}', fileName: '{fileName ?? string.Empty}'.");
    }

    private static string ResolveExtension(string fileUrl, string fileName, string mediaType)
    {
        string extension = null;

        if (Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
            extension = Path.GetExtension(uri.AbsolutePath);

        if (string.IsNullOrWhiteSpace(extension) && !string.IsNullOrWhiteSpace(fileName))
            extension = Path.GetExtension(fileName);

        if (!string.IsNullOrWhiteSpace(extension))
            return extension.ToLowerInvariant();

        return mediaType?.ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            "application/vnd.ms-excel" => ".xls",
            "text/plain" => ".txt",
            "text/markdown" => ".md",
            "text/csv" => ".csv",
            "application/json" => ".json",
            "application/xml" => ".xml",
            "text/xml" => ".xml",
            "text/html" => ".html",
            _ => null
        };
    }

    private static string ExtractByExtension(string ext, byte[] bytes)
    {
        if (!SupportedExtensions.Contains(ext))
            throw new NotSupportedException($"Unsupported file extension: '{ext}'.");

        if (TextExtensions.Contains(ext))
            return DecodeText(bytes);

        return ext switch
        {
            ".pdf" => ExtractTextFromPdf(bytes),
            ".docx" => ExtractTextFromWord(bytes),
            ".xlsx" => ExtractTextFromExcel(bytes),
            ".xls" => ExtractTextFromXls(bytes),
            _ => throw new NotSupportedException($"Unsupported file extension: '{ext}'.")
        };
    }

    private static string DecodeText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        return reader.ReadToEnd();
    }

    private static string ExtractTextFromWord(byte[] content)
    {
        using var data = new MemoryStream(content);
        using var doc = WordprocessingDocument.Open(data, false);

        var body = doc.MainDocumentPart?.Document?.Body;
        return body == null ? string.Empty : body.InnerText;
    }

    private static string ExtractTextFromPdf(byte[] content)
    {
        var sb = new StringBuilder();
        using var data = new MemoryStream(content);
        using var pdf = PdfDocument.Open(data);
        foreach (var page in pdf.GetPages())
        {
            var pageText = page.Text ?? string.Empty;
            if (pageText.Length == 0)
                continue;

            sb.AppendLine(pageText);
            sb.AppendLine();
        }

        return sb.ToString().Trim();
    }

    private static string ExtractTextFromExcel(byte[] content)
    {
        using var workbook = new XLWorkbook(new MemoryStream(content));

        var sb = new StringBuilder();
        foreach (var worksheet in workbook.Worksheets)
        {
            foreach (var row in worksheet.RangeUsed().RowsUsed())
            {
                var cells = row.CellsUsed().ToList();
                for (var i = 0; i < cells.Count; i++)
                {
                    if (i > 0) sb.Append('\t');
                    sb.Append(cells[i].Value);
                }

                sb.AppendLine();
            }

            sb.AppendLine();
        }

        return sb.ToString().Trim();
    }

    private static string ExtractTextFromXls(byte[] content)
    {
        using var stream = new MemoryStream(content);
        IWorkbook workbook = new HSSFWorkbook(stream);

        var sb = new StringBuilder();
        for (var sheetIndex = 0; sheetIndex < workbook.NumberOfSheets; sheetIndex++)
        {
            var sheet = workbook.GetSheetAt(sheetIndex);
            if (sheet == null) continue;

            for (var rowIndex = sheet.FirstRowNum; rowIndex <= sheet.LastRowNum; rowIndex++)
            {
                var row = sheet.GetRow(rowIndex);
                if (row == null) continue;

                var firstCellIndex = row.FirstCellNum;
                var lastCellIndex = row.LastCellNum;
                if (firstCellIndex < 0 || lastCellIndex < 0) continue;

                for (var cellIndex = firstCellIndex; cellIndex < lastCellIndex; cellIndex++)
                {
                    if (cellIndex > firstCellIndex) sb.Append('\t');
                    sb.Append(row.GetCell(cellIndex)?.ToString() ?? string.Empty);
                }

                sb.AppendLine();
            }

            sb.AppendLine();
        }

        return sb.ToString().Trim();
    }
}
