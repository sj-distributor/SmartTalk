using SmartTalk.Messages.Enums.MessageLogging;

namespace SmartTalk.Messages.Attributes;

public class SmartTalkLoggingAttribute : Attribute
{
    public LoggingSystemType Type { get; set; }

    public SmartTalkLoggingAttribute(LoggingSystemType type)
    {
        Type = type;
    }
}