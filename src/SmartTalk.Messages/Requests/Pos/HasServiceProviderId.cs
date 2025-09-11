namespace SmartTalk.Messages.Requests.Pos;

public class HasServiceProviderId
{
    public int? ServiceProviderId { get; set; }
}

public class HasServiceProviderId<T>
{
    public T ServiceProviderId { get; set; }
}