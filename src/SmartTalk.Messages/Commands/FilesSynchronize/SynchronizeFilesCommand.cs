using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.FilesSynchronize;

public class SynchronizeFilesCommand : ICommand
{
    public SynchronizeFilesData Source { get; set; }
    
    public List<SynchronizeFilesDestinationData> Destinations { get; set; }
}

public class SynchronizeFilesData
{
    public string User { get; set; }
    
    public string Server { get; set; }
    
    public string Path { get; set; }
}

public class SynchronizeFilesDestinationData : SynchronizeFilesData
{
    public List<string> ExcludeFiles { get; set; }
}