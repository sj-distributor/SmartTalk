using Newtonsoft.Json;
using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.Linphone;

public class AddLinphoneCdrCommand : ICommand
{
    public string RecordName { get; set; }
}