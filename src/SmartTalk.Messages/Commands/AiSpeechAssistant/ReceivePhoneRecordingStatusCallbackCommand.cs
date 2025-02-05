using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class ReceivePhoneRecordingStatusCallbackCommand : ICommand
{
    public string CallSid { get; set; }
    
    public string RecordingSid { get; set; }
    
    public string RecordingUrl { get; set; }
    
    public string RecordingStatus { get; set; }
    
    public string RecordingTrack { get; set; }
}