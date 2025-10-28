using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AutoTest;

public class AutoTestConversationAudioProcessCommand : ICommand
{
    public List<byte[]> CustomerAudioList { get; set; }
    
    public string Prompt { get; set; }
}

public class AutoTestConversationAudioProcessReponse : SmartTalkResponse<byte[]>
{
}