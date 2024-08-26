using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Messages.Commands.PhoneOrder;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderService
{
    Task<GetPhoneOrderOrderItemsRessponse> GetPhoneOrderOrderItemsAsync(GetPhoneOrderOrderItemsRequest request, CancellationToken cancellationToken);

    Task<AddPhoneOrderOrderItemsResponse> AddPhoneOrderOrderItemsAsync(AddPhoneOrderOrderItemsCommand command, CancellationToken cancellationToken);
}

public partial class PhoneOrderService
{
    public async Task<GetPhoneOrderOrderItemsRessponse> GetPhoneOrderOrderItemsAsync(GetPhoneOrderOrderItemsRequest request, CancellationToken cancellationToken)
    {
        var orderItems = await _phoneOrderDataProvider.GetPhoneOrderOrderItemsAsync(request.RecordId, cancellationToken).ConfigureAwait(false);

        return new GetPhoneOrderOrderItemsRessponse
        {
            Data = new GetPhoneOrderOrderItemsData
            {
                ManualItems = _mapper.Map<List<PhoneOrderOrderItemDto>>(orderItems.Where(x => x.OrderType == PhoneOrderOrderType.ManualOrder).ToList()),
                AIItems = _mapper.Map<List<PhoneOrderOrderItemDto>>(orderItems.Where(x => x.OrderType == PhoneOrderOrderType.AIOrder).ToList())
            }
        };
    }

    public async Task<AddPhoneOrderOrderItemsResponse> AddPhoneOrderOrderItemsAsync(AddPhoneOrderOrderItemsCommand command, CancellationToken cancellationToken)
    {
        var orderItems = _mapper.Map<List<PhoneOrderOrderItem>>(command.OrderItems);

        await _phoneOrderDataProvider.AddPhoneOrderOrderItemsAsync(orderItems, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new AddPhoneOrderOrderItemsResponse
        {
            Data = _mapper.Map<List<PhoneOrderOrderItemDto>>(orderItems)
        };
    }
}