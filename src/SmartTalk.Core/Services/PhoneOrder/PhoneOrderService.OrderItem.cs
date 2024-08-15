using AutoMapper;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Requests;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderService
{
    Task<GetPhoneOrderOrderItemsRessponse> GetPhoneOrderOrderItemsAsync(GetPhoneOrderOrderItemsRequest request, CancellationToken cancellationToken);
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
}