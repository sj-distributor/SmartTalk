using SmartTalk.Messages.Enums.Security;

namespace SmartTalk.Messages.Dto.Security;

public class UnauthorizedTargetDto
{
    public string Name { get; set; }

    public UnauthorizedTargetType Type { get; set; }
}