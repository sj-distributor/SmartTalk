namespace SmartTalk.Messages.Requests.Pos;

public class PosHasServiceId
{
    public int PosServiceId { get; set; }
}

public class PosHasServiceId<T>
{
    public T PosServiceId { get; set; }
}