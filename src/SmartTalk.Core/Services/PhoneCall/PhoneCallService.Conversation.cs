using SmartTalk.Core.Domain.PhoneCall;
using SmartTalk.Messages.Dto.PhoneCall;
using SmartTalk.Messages.Commands.PhoneCall;
using SmartTalk.Messages.Requests.PhoneCall;

namespace SmartTalk.Core.Services.PhoneCall;

public partial interface IPhoneCallService
{
    Task<GetPhoneCallConversationsResponse> GetPhoneOrderConversationsAsync(GetPhoneCallConversationsRequest request, CancellationToken cancellationToken);

    Task<AddPhoneOrderConversationsResponse> AddPhoneOrderConversationsAsync(AddPhoneCallConversationsCommand command, CancellationToken cancellationToken);
}

public partial class PhoneCallService
{
    public async Task<GetPhoneCallConversationsResponse> GetPhoneOrderConversationsAsync(GetPhoneCallConversationsRequest request, CancellationToken cancellationToken)
    {
        var conversations = await _phoneCallDataProvider.GetPhoneOrderConversationsAsync(request.RecordId, cancellationToken).ConfigureAwait(false);

        return new GetPhoneCallConversationsResponse
        {
            Data = _mapper.Map<List<PhoneCallConversationDto>>(conversations)
        };
    }

    public async Task<AddPhoneOrderConversationsResponse> AddPhoneOrderConversationsAsync(AddPhoneCallConversationsCommand command, CancellationToken cancellationToken)
    {
        if (!command.Conversations.Any()) throw new Exception("Phone order conversations could not be null");
        
        await _phoneCallDataProvider.DeletePhoneOrderConversationsAsync(command.Conversations.First().RecordId, cancellationToken).ConfigureAwait(false);
        
        var conversations = await _phoneCallDataProvider.AddPhoneOrderConversationsAsync(_mapper.Map<List<PhoneCallConversation>>(command.Conversations), cancellationToken: cancellationToken).ConfigureAwait(false);

        var record = (await _phoneCallDataProvider.GetPhoneCallRecordAsync(command.Conversations.First().RecordId, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        
        if (_currentUser.Id.HasValue)
            await UpdatePhoneOrderRecordSpecificFieldsAsync(command.Conversations.First().RecordId, _currentUser.Id.Value, command.Conversations.First().Question, _currentUser.Name, cancellationToken).ConfigureAwait(false);

        var conversationsList = string.Concat(string.Join(",", conversations.Select(x => x.Question)), string.Join(",", conversations.Select(x => x.Answer)));

        await _phoneCallUtilService.ExtractPhoneOrderShoppingCartAsync(conversationsList, record, cancellationToken).ConfigureAwait(false);

        return new AddPhoneOrderConversationsResponse
        {
            Data = _mapper.Map<List<PhoneCallConversationDto>>(conversations)
        };
    }
}