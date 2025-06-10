using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;

namespace SmartTalk.Messages.Commands.Pos;

public class ModifyPosMenuCommand : ICommand
{
    public long PosId { get; set; }

    public List<PosModifiedDto> ModifiedContents { get; set; }
}