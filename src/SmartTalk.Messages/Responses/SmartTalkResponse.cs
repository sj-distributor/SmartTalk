using System.Net;
using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Responses;

public class SmartTalkResponse<T> : SmartTalkResponse
{
    public T Data { get; set; }
}

public class SmartTalkResponse : IResponse
{
    public HttpStatusCode Code { get; set; }

    public string Msg { get; set; }
}