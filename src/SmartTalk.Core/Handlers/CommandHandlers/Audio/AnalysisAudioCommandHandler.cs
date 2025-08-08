using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Audio;
using SmartTalk.Messages.Commands.SpeechMatics;

namespace SmartTalk.Core.Handlers.CommandHandlers.Audio;

public class AnalysisAudioCommandHandler : ICommandHandler<AnalyzeAudioCommand, AnalyzeAudioResponse>
{
    private readonly IAudioService _audioService;

    public AnalysisAudioCommandHandler(IAudioService audioService)
    {
        _audioService = audioService;
    }

    public async Task<AnalyzeAudioResponse> Handle(IReceiveContext<AnalyzeAudioCommand> context, CancellationToken cancellationToken)
    {
        return await _audioService.AnalyzeAudioAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}