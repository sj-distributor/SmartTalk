namespace SmartTalk.Messages.Dto.Printer;

public class PrinterJobDto
{
    public string Mac { get; set; }

    public string Type { get; set; }

    public Guid Token { get; set; }
}