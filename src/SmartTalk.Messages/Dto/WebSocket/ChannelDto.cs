using Newtonsoft.Json;

namespace SmartTalk.Messages.Dto.WebSocket;

public class ChannelDto
{
    public string Id { get; set; }
    
    public string Name { get; set; }
    
    public string State { get; set; }
    
    [JsonProperty("protocol_id")]
    public string ProtocolId { get; set; }
    
    public CallerDto Caller { get; set; }
    
    public ConnectedDto Connected { get; set; }
    
    public string AccountCode { get; set; }
    
    public DialplanDto Dialplan { get; set; }
    
    public DateTime CreationTime { get; set; }
    
    public string Language { get; set; }

    // 嵌套的Caller信息
    public class CallerDto
    {
        public string Name { get; set; }
        
        public string Number { get; set; }
    }

    // 嵌套的Connected信息
    public class ConnectedDto
    {
        public string Name { get; set; }
        
        public string Number { get; set; }
    }

    // 嵌套的Dialplan信息
    public class DialplanDto
    {
        public string Context { get; set; }
        
        public string Exten { get; set; }
        
        public int Priority { get; set; }
        
        [JsonProperty("app_name")]
        public string AppName { get; set; }
        
        [JsonProperty("app_data")]
        public string AppData { get; set; }
    }
}