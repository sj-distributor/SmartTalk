using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AutoTest;

public class AutoTestConversationAudioProcessCommand : ICommand
{
    public List<byte[]> CustomerAudioList { get; set; }
    
    public string Prompt { get; set; }
}

public class AutoTestConversationAudioProcessReponse : SmartTalkResponse<List<AudioConversationRecord>>
{
}

public class AudioConversationRecord
{
    public byte[] UserAudio { get; set; }
    
    public byte[] AiAudio { get; set; }
    
    public string AiText { get; set; }
}