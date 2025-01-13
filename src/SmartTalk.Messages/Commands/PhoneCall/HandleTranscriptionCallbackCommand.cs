using SmartTalk.Messages.Dto.PhoneCall;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Responses;
using ICommand = Mediator.Net.Contracts.ICommand;

namespace SmartTalk.Messages.Commands.PhoneCall;

public class HandleTranscriptionCallbackCommand : ICommand
{
    public SpeechMaticsGetTranscriptionResponseDto Transcription { get; set; }
}