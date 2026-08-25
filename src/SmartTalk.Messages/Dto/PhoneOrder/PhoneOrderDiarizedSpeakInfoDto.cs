using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Messages.Dto.PhoneOrder;

public class PhoneOrderDiarizedSpeakInfoDto
{
    public double StartTime { get; set; }

    public double EndTime { get; set; }

    public string Speaker { get; set; }

    public PhoneOrderRole? Role { get; set; }

    public string RoleText { get; set; }

    public string Text { get; set; }
}
