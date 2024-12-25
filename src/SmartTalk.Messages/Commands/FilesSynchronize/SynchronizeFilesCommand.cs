using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.FilesSynchronize;

public class SynchronizeFilesCommand : ICommand
{
    public SynchronizeFilesData Source { get; set; }
    
    public List<SynchronizeFilesDestinationData> Destinations { get; set; }
}

public abstract class SynchronizeFilesBaseData
{
    public string User { get; set; }
    
    public string Server { get; set; }
    
    public string Path { get; set; }
    
    public string ServerPath
    {
        get => $"{User}@{Server}:{Path}";
        
        set
        {
            if (string.IsNullOrWhiteSpace(value)) 
                throw new ArgumentException("ServerPath cannot be null or empty.");

            var parts = value.Split(new[] { "@", ":", "-" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            
            if (parts.Count < 3)
                throw new FormatException("ServerPath must be in the format 'user@server:path'.");

            User = parts[0];
            Server = parts[1];
            Path = parts[2];

            OnServerPathParsed(parts.Skip(3).ToList());
        }
    }
    
    protected virtual void OnServerPathParsed(List<string> extraParts) { }
}

public class SynchronizeFilesData : SynchronizeFilesBaseData
{
}

public class SynchronizeFilesDestinationData : SynchronizeFilesBaseData
{
    public List<string> ExcludeFiles { get; set; } = [];

    protected override void OnServerPathParsed(List<string> extraParts) { ExcludeFiles = extraParts; }
}


