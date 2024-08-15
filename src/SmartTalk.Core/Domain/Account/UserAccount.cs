using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Core.Domain.Security;
using SmartTalk.Messages.Enums.Account;

namespace SmartTalk.Core.Domain.Account
{
    [Table("user_account")]
    public class UserAccount : IEntity
    {
        public UserAccount()
        {
            Uuid = Guid.NewGuid();
            CreatedOn = DateTime.Now;
            ModifiedOn = DateTime.Now;
        }
        
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        [Column("created_on")]
        public DateTime CreatedOn { get; set; }
    
        [Column("modified_on")]
        public DateTime ModifiedOn { get; set; }
        
        [Column("uuid", TypeName = "varchar(36)")]
        public Guid Uuid { get; set; }
        
        [Column("username")]
        [StringLength(512)]
        public string UserName { get; set; }
    
        [Column("password")]
        [StringLength(128)]
        public string Password { get; set; }
    
        [Column("third_party_user_id")]
        [StringLength(128)]
        public string ThirdPartyUserId { get; set; }
        
        [Column("issuer")]
        public UserAccountIssuer Issuer { get; set; }
        
        [Column("active", TypeName = "tinyint(1)")]
        public bool IsActive { get; set; }
        
        [NotMapped]
        public List<Role> Roles { get; set; } = new();

        [NotMapped]
        public List<Permission> Permissions { get; set; } = new();
    
        [NotMapped]
        public UserAccountProfile UserAccountProfile { get; set; }
    }
}