using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.SipServer;

public class SipBackupServerDto
{
    public int Id { get; set; }

    public int HostId { get; set; }

    public string UserName { get; set; }

    public string ServerIp { get; set; }
    
    public string DestinationPath { get; set; }
    
    public string ExcludeFiles { get; set; }

    public DateTimeOffset CreatedDate { get; set; }

    public DateTimeOffset? LastModifiedDate { get; set; }
    
    [JsonIgnore]
    public string ServerPath => $"{UserName}@{ServerIp}:{DestinationPath}";
}