using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.FilesSynchronize;
using SmartTalk.Messages.Commands.FilesSynchronize;

namespace SmartTalk.Core.Handlers.CommandHandlers.FilesSynchronize;

public class SynchronizeFilesCommandHandler : ICommandHandler<SynchronizeFilesCommand>
{
    private readonly IFilesSynchronizeService _filesSynchronizeService;

    public SynchronizeFilesCommandHandler(IFilesSynchronizeService filesSynchronizeService)
    {
        _filesSynchronizeService = filesSynchronizeService;
    }
    
    public async Task Handle(IReceiveContext<SynchronizeFilesCommand> context, CancellationToken cancellationToken)
    {
        await _filesSynchronizeService.SynchronizeFilesAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}