namespace SmartTalk.Core.Services.AiSpeechAssistant;

using global::System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using SmartTalk.Core.Ioc;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

public interface IFileTextExtractor : IScopedDependency
{
    Task<string> ExtractAsync(string fileUrl, CancellationToken cancellationToken);
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
        ".pdf", ".docx", ".xlsx"
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public FileTextExtractor(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> ExtractAsync(string fileUrl, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient();
        using var response = await client.GetAsync(fileUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes == null || bytes.Length == 0)
            return string.Empty;

        if (Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            var ext = Path.GetExtension(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(ext))
                return ExtractByExtension(ext, bytes);
        }

        throw new NotSupportedException($"Unsupported file URL: '{fileUrl}'.");
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
        using var data = new MemoryStream(content);
        using var pdf = PdfDocument.Open(data);

        var sb = new StringBuilder();
        foreach (Page page in pdf.GetPages())
        {
            var pageText = ContentOrderTextExtractor.GetText(page) ?? string.Empty;
            if (pageText.Length == 0) continue;

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
}
