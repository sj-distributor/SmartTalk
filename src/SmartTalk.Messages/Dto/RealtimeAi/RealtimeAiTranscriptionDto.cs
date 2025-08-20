using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Messages.Dto.RealtimeAi;

public class RealtimeAiTranscriptionDto
{
    public string Transcription { get; set; }
    
    public AiSpeechAssistantSpeaker Speaker { get; set; }
}