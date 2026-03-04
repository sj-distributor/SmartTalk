using ICommand = Mediator.Net.Contracts.ICommand;

namespace SmartTalk.Messages.Commands.AutoTest;

public class AutoTestImportJobCommand : ICommand
{
    public string CustomerId { get; set; }
    
    public DateTime StartDate { get; set; }
    
    public DateTime EndDate { get; set; }
    
    public int ScenarioId { get; set; }
}