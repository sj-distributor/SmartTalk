using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.Linphone;

public class GetLinphoneCdrResponseDto
{
    [JsonProperty("data")]
    public List<LinphoneCdrDto> Cdrs { get; set; }
}

public class LinphoneCdrDto
{
    [JsonProperty("calldate")]
    public string CallDate { get; set; }

    [JsonProperty("clid")]
    public string ClId { get; set; }

    [JsonProperty("src")]
    public string Src { get; set; }

    [JsonProperty("dst")]
    public string Dst { get; set; }

    [JsonProperty("dcontext")]
    public string Dcontext { get; set; }

    [JsonProperty("channel")]
    public string Channel { get; set; }

    [JsonProperty("dstchannel")]
    public string Dstchannel { get; set; }

    [JsonProperty("lastapp")]
    public string LastApp { get; set; }

    [JsonProperty("lastdata")]
    public string LastData { get; set; }

    [JsonProperty("duration")]
    public string Duration { get; set; }

    [JsonProperty("billsec")]
    public string BillSec { get; set; }

    [JsonProperty("disposition")]
    public string Disposition { get; set; }

    [JsonProperty("amaflags")]
    public string Amaflags { get; set; }

    [JsonProperty("accountcode")]
    public string Accountcode { get; set; }

    [JsonProperty("uniqueid")]
    public string Uniqueid { get; set; }

    [JsonProperty("userfield")]
    public string Userfield { get; set; }

    [JsonProperty("did")]
    public string Did { get; set; }

    [JsonProperty("recordingfile")]
    public string RecordingFile { get; set; }

    [JsonProperty("cnum")] 
    public string Cnum { get; set; }

    [JsonProperty("cnam")] 
    public string Cname { get; set; }

    [JsonProperty("outbound_cnum")]
    public string OutBoundCnum { get; set; }

    [JsonProperty("outbound_cnam")]
    public string OutboundCnam { get; set; }

    [JsonProperty("dst_cnam")]
    public string DstCnam { get; set; }

    [JsonProperty("linkedid")]
    public string Linkedid { get; set; }

    [JsonProperty("peeraccount")]
    public string PeerAccount { get; set; }

    [JsonProperty("sequence")]
    public string Sequence { get; set; }
}