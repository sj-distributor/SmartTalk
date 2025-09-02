namespace SmartTalk.Messages.Requests.Pos;

public class HasServiceProviderId
{
    public int? ServiceProviderId { get; set; }
}

public class PosHasServiceProviderId<T>
{
    public T ServiceProviderId { get; set; }
}