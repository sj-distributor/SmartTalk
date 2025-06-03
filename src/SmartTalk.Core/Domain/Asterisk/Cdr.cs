using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.Asterisk;

[Table("cdr")]
public class Cdr : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("call_date")]
    public string CallDate { get; set; }

    [Column("clid")]
    public string ClId { get; set; }

    [Column("src")]
    public string Src { get; set; }

    [Column("dst")]
    public string Dst { get; set; }

    [Column("dcontext")]
    public string Dcontext { get; set; }

    [Column("channel")]
    public string Channel { get; set; }

    [Column("dstchannel")]
    public string Dstchannel { get; set; }

    [Column("lastapp")]
    public string LastApp { get; set; }

    [Column("lastdata")]
    public string LastData { get; set; }

    [Column("duration")]
    public string Duration { get; set; }

    [Column("billsec")]
    public string BillSec { get; set; }

    [Column("disposition")]
    public string Disposition { get; set; }

    [Column("amaflags")]
    public string Amaflags { get; set; }

    [Column("accountcode")]
    public string Accountcode { get; set; }

    [Column("uniqueid")]
    public string Uniqueid { get; set; }

    [Column("userfield")]
    public string Userfield { get; set; }

    [Column("did")]
    public string Did { get; set; }

    [Column("recordingfile")]
    public string RecordingFile { get; set; }

    [Column("cnum")] 
    public string Cnum { get; set; }

    [Column("cnam")] 
    public string Cname { get; set; }

    [Column("outbound_cnum")]
    public string OutBoundCnum { get; set; }

    [Column("outbound_cnam")]
    public string OutboundCnam { get; set; }

    [Column("dst_cnam")]
    public string DstCnam { get; set; }

    [Column("linkedid")]
    public string Linkedid { get; set; }

    [Column("peeraccount")]
    public string PeerAccount { get; set; }

    [Column("sequence")]
    public string Sequence { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}