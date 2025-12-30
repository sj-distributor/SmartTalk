using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.PhoneOrder;

public class UpdatePhoneOrderRecordCommand: ICommand
{
    public int RecordId { get; set; }

    public DialogueScenarios DialogueScenarios { get; set; }
    
    public int UserId { get; set; }
}

public class UpdatePhoneOrderRecordResponse : SmartTalkResponse<UpdatePhoneOrderRecordResponseDate>
{
}

public class UpdatePhoneOrderRecordResponseDate
{
    public int RecordId { get; set; }

    public DialogueScenarios DialogueScenarios { get; set; }

    public string UserName { get; set; }
}