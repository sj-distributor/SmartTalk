using System.ComponentModel.DataAnnotations;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Twilio;

public class MigrateIncomingPhoneNumberRequest : IRequest
{
    [Required]
    public string PhoneNumberSid { get; set; }

    [Required]
    public string LosingAccountSid { get; set; }

    [Required]
    public string GainingAccountSid { get; set; }

    public string BundleSid { get; set; }

    public string AddressSid { get; set; }
}

public class MigrateIncomingPhoneNumberResponse : SmartTalkResponse
{
    public string Sid { get; set; }

    public string AccountSid { get; set; }

    public string PhoneNumber { get; set; }

    public string FriendlyName { get; set; }

    public string BundleSid { get; set; }

    public string AddressSid { get; set; }

    public DateTimeOffset? DateUpdated { get; set; }

    public int? Code { get; set; }

    public string Message { get; set; }

    public string MoreInfo { get; set; }

    public int? Status { get; set; }
}
