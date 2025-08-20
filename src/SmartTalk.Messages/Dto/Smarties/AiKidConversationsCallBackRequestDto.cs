using SmartTalk.Messages.Dto.RealtimeAi;

namespace SmartTalk.Messages.Dto.Smarties;

public class AiKidConversationCallBackRequestDto
{
    public Guid Uuid { get; set; }
    
    public List<RealtimeAiTranscriptionDto> Transcriptions { get; set; }
}