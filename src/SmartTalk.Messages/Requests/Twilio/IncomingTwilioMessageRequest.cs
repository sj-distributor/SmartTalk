namespace SmartTalk.Messages.Requests.Twilio;

// Twilio posts these fields as application/x-www-form-urlencoded data.
public class IncomingTwilioMessageRequest
{
    public string MessageSid { get; set; }

    public string From { get; set; }

    public string To { get; set; }

    public string Body { get; set; }
}
