using SmartTalk.Messages.Dto.PhoneCall;
using SmartTalk.Messages.Enums.PhoneCall;
using SmartTalk.Messages.Requests.PhoneCall;

namespace SmartTalk.Core.Services.PhoneCall;

public partial interface IPhoneCallService
{
    Task<GetPhoneCallOrderItemsRessponse> GetPhoneOrderOrderItemsAsync(GetPhoneCallOrderItemsRequest request, CancellationToken cancellationToken);
}

public partial class PhoneCallService
{
    public async Task<GetPhoneCallOrderItemsRessponse> GetPhoneOrderOrderItemsAsync(GetPhoneCallOrderItemsRequest request, CancellationToken cancellationToken)
    {
        var orderItems = await _phoneCallDataProvider.GetPhoneOrderOrderItemsAsync(request.RecordId, cancellationToken: cancellationToken).ConfigureAwait(false);

        var record = (await _phoneCallDataProvider.GetPhoneCallRecordAsync(request.RecordId, cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        
        return new GetPhoneCallOrderItemsRessponse
        {
            Data = new GetPhoneCallOrderItemsData
            {
                ManualOrderId = record.ManualOrderId.ToString(),
                ManualItems = _mapper.Map<List<PhoneCallOrderItemDto>>(orderItems.Where(x => x.CallOrderType == PhoneCallOrderType.ManualOrder).ToList()),
                AIItems = _mapper.Map<List<PhoneCallOrderItemDto>>(orderItems.Where(x => x.CallOrderType == PhoneCallOrderType.AIOrder).ToList())
            }
        };
    }
}