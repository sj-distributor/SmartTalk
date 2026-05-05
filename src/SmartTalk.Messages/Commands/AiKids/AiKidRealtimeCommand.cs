using System.Net.WebSockets;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Messages.Commands.AiKids;

public class AiKidRealtimeCommand : ICommand
{
    public int AssistantId { get; set; }
    
    public WebSocket WebSocket { get; set; }
    
    public RealtimeAiAudioCodec InputFormat { get; set; }
    
    public RealtimeAiAudioCodec OutputFormat { get; set; }
    
    public RealtimeAiServerRegion Region { get; set; }
    
    public PhoneOrderRecordType OrderRecordType { get; set; }

    public bool SuppressGreeting { get; set; }

    public bool DisableIdleFollowUp { get; set; }

    public bool RecordTextInputAsTranscription { get; set; }

    public Func<string, CancellationToken, Task<RealtimeTextRecordingAudio>> TextInputRecordingAudioProviderAsync { get; set; }

    public Func<string, string, Task> OnRecordingUploadedAsync { get; set; }
}
