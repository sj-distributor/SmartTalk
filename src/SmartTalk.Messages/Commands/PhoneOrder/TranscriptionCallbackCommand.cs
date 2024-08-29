using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Dto.Speechmatics;
using SmartTalk.Messages.Responses;
using ICommand = Mediator.Net.Contracts.ICommand;

namespace SmartTalk.Messages.Commands.PhoneOrder;

public class TranscriptionCallbackCommand : ICommand
{
    public SpeechmaticsGetTranscriptionResponseDto Transcription { get; set; }
}

public class TranscriptionCallbackResponse : SmartTalkResponse<List<PhoneOrderConversationDto>>
{
}