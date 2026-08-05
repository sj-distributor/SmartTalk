using System.ComponentModel.DataAnnotations;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Twilio;

public class SendTwilioMessageRequest : IRequest
{
    [Required]
    public string FromNumber { get; set; }

    [Required]
    public string ToNumber { get; set; }

    [Required]
    public string Body { get; set; }
}

public class SendTwilioMessageResponse : SmartTalkResponse
{
    public string Sid { get; set; }

    public string AccountSid { get; set; }

    public string From { get; set; }

    public string To { get; set; }

    public string Body { get; set; }

    public string Status { get; set; }

    public int? ErrorCode { get; set; }

    public string ErrorMessage { get; set; }
}