using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Responses;
using ICommand = Mediator.Net.Contracts.ICommand;

namespace SmartTalk.Messages.Commands.PhoneOrder;

public class HandleTranscriptionCallbackCommand : ICommand
{
    public SpeechMaticsGetTranscriptionResponseDto Transcription { get; set; }
}