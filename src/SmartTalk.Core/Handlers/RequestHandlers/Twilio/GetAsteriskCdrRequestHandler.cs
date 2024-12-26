using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Messages.Requests.Twilio;

namespace SmartTalk.Core.Handlers.RequestHandlers.Twilio;

public class GetAsteriskCdrRequestHandler : IRequestHandler<GetAsteriskCdrRequest, GetAsteriskCdrResponse>
{
    private IAsteriskClient _asteriskClient;

    public GetAsteriskCdrRequestHandler(IAsteriskClient asteriskClient)
    {
        _asteriskClient = asteriskClient;
    }
    
    public async Task<GetAsteriskCdrResponse> Handle(IReceiveContext<GetAsteriskCdrRequest> context, CancellationToken cancellationToken)
    {
        return new GetAsteriskCdrResponse
        {
            Data = await _asteriskClient.GetAsteriskCdrAsync(context.Message.Number, cancellationToken).ConfigureAwait(false)
        };
    }
}