using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.AutoTest;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AutoTest;

public class AutoTestImportDataCommand : ICommand
{
    public byte[] FileBytes { get; set; }
    
    public AutoTestImportDataRecordType ImportType { get; set; }
}

public class AutoTestImportDataResponse : SmartTalkResponse
{
}