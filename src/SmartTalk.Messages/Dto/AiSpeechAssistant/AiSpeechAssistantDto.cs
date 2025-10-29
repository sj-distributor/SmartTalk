using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiSpeechAssistantDto
{
    public int Id { get; set; }
    
    public string Name { get; set; }
    
    public int AnsweringNumberId { get; set; }
    
    public string AnsweringNumber { get; set; }
    
    public string ModelUrl { get; set; }
    
    public AiSpeechAssistantProvider ModelProvider { get; set; }
    
    public string ModelVoice { get; set; }
    
    public string ModelLanguage { get; set; }
    
    public string CustomRecordAnalyzePrompt { get; set; }
    
    public bool ManualRecordWholeAudio { get; set; }
    
    public string CustomRepeatOrderPrompt { get; set; }
    
    public string Channel { get; set; }
    
    public bool IsDisplay { get; set; }
    
    public int WaitInterval { get; set; }
    
    public bool IsTransferHuman { get; set; }
    
    public string TransferCallNumber { get; set; }
    
    public bool IsDefault { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
    
    public int CreatedBy { get; set; }
    
    public AiSpeechAssistantKnowledgeDto Knowledge { get; set; }

    public List<AiSpeechAssistantChannel> Channels => string.IsNullOrWhiteSpace(Channel)
        ? [] : Channel.Split(',').Select(x => Enum.TryParse(x, out AiSpeechAssistantChannel channel)
                ? channel : (AiSpeechAssistantChannel?)null).Where(x => x.HasValue).Select(x => x!.Value).ToList();
}