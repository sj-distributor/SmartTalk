using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.SipServer;

[Table("sip_backup_server")]
public class SipBackupServer : IEntity,IHasCreatedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("host_id")]
    public int HostId { get; set; }

    [Column("user_name"), StringLength(255)]
    public string UserName { get; set; }

    [Column("server_ip"), StringLength(255)]
    public string ServerIp { get; set; }
    
    [Column("destination_path"), StringLength(1024)]
    public string DestinationPath { get; set; }
    
    [Column("exclude_files")]
    public string ExcludeFiles { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }

    [Column("last_modified_date")]
    public DateTimeOffset? LastModifiedDate { get; set; }
    
    [NotMapped]
    public string ServerPath => $"{UserName}@{ServerIp}:{DestinationPath}";
}