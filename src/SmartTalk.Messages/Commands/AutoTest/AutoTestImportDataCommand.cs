using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AutoTest;

public class AutoTestImportDataCommand : ICommand
{
    public Dictionary<string, object> ImportData { get; set; }
    
    public int ScenarioId { get; set; }
    
    public string KeyName { get; set; }
    
    public AutoTestImportDataRecordType ImportType { get; set; }
}

public class AutoTestImportDataResponse : SmartTalkResponse<AutoTestDataSetDto>
{
}