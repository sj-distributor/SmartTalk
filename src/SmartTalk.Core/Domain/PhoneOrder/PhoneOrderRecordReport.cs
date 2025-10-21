using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums;
using SmartTalk.Messages.Enums.STT;

namespace SmartTalk.Core.Domain.PhoneOrder;

[Table("phone_order_record_report")]
public class PhoneOrderRecordReport : IEntity
{
   [Key]
   [Column("id")]
   [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
   public int Id { get; set; }

   [Column("record_id")]
   public int RecordId { get; set; }

   [Column("language")]
   public TranscriptionLanguage Language { get; set; }

   [Column("report")]
   public string Report { get; set; }
   
   [Column("is_origin")]
   public bool IsOrigin { get; set; }
   
   [Column("is_customer_friendly")]
   public bool IsCustomerFriendly { get; set; }
   
   [Column("created_date")]
   public DateTimeOffset CreatedDate { get; set; }
}