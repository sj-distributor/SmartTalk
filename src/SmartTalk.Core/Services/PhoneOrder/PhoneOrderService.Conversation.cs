using System.Net;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Messages.Commands;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Requests;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderService
{
    Task<GetPhoneOrderConversationsResponse> GetPhoneOrderConversationsAsync(GetPhoneOrderConversationsRequest request, CancellationToken cancellationToken);

    Task<AddPhoneOrderConversationsResponse> AddPhoneOrderConversationsAsync(AddPhoneOrderConversationsCommand command, CancellationToken cancellationToken);
}

public partial class PhoneOrderService
{
    public async Task<GetPhoneOrderConversationsResponse> GetPhoneOrderConversationsAsync(GetPhoneOrderConversationsRequest request, CancellationToken cancellationToken)
    {
        var conversations = await _phoneOrderDataProvider.GetPhoneOrderConversationsAsync(request.RecordId, cancellationToken).ConfigureAwait(false);

        return new GetPhoneOrderConversationsResponse
        {
            Data = _mapper.Map<List<PhoneOrderConversationDto>>(conversations)
        };
    }

    public async Task<AddPhoneOrderConversationsResponse> AddPhoneOrderConversationsAsync(AddPhoneOrderConversationsCommand command, CancellationToken cancellationToken)
    {
        if (!command.Conversations.Any()) throw new Exception("Phone order conversations could not be null");
        
        await _phoneOrderDataProvider.DeletePhoneOrderConversationsAsync(command.Conversations.First().RecordId, cancellationToken).ConfigureAwait(false);
        
        var conversations = await _phoneOrderDataProvider.AddPhoneOrderConversationsAsync(_mapper.Map<List<PhoneOrderConversation>>(command.Conversations), cancellationToken: cancellationToken).ConfigureAwait(false);

        return new AddPhoneOrderConversationsResponse
        {
            Data = _mapper.Map<List<PhoneOrderConversationDto>>(conversations)
        };
    }
}