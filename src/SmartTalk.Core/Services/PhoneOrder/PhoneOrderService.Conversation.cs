using Serilog;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;
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

        var record = (await _phoneOrderDataProvider.GetPhoneOrderRecordAsync(command.Conversations.First().RecordId, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        
        if (_currentUser?.Id.HasValue != true) throw new Exception("Current user is not authenticated.");

        await UpdatePhoneOrderRecordSpecificFieldsAsync(command.Conversations.First().RecordId, _currentUser.Id.Value, command.Conversations.First().Question, _currentUser.Name, cancellationToken).ConfigureAwait(false);

        var conversationsList = string.Concat(string.Join(",", conversations.Select(x => x.Question)), string.Join(",", conversations.Select(x => x.Answer)));

        var orderItems = await _phoneOrderDataProvider.GetPhoneOrderOrderItemsAsync(command.Conversations.First().RecordId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (orderItems.Any())
        {
            await _phoneOrderDataProvider.DeletePhoneOrderItemsAsync(orderItems,true, cancellationToken).ConfigureAwait(false);
            
            Log.Information("Delete Phone Order Items When Add Phone Order Conversations {@orderItems}", orderItems);
        }
     
        await _phoneOrderUtilService.ExtractPhoneOrderShoppingCartAsync(conversationsList, record, cancellationToken).ConfigureAwait(false);

        return new AddPhoneOrderConversationsResponse
        {
            Data = _mapper.Map<List<PhoneOrderConversationDto>>(conversations)
        };
    }
}