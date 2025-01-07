using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.SipServer;

public class SipHostServerDto
{
    public int Id { get; set; }

    public string UserName { get; set; }

    public string ServerIp { get; set; }
    
    public string SourcePath { get; set; }

    public DateTimeOffset CreatedDate { get; set; }

    public DateTimeOffset? LastModifiedDate { get; set; }
    
    public List<SipBackupServerDto> BackupServers { get; set; }
}