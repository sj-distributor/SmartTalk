using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.Pos;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Services.Pos;

public partial interface IPosService : IScopedDependency
{
    Task<GetStoreCustomersResponse> GetStoreCustomersAsync(GetStoreCustomersRequest request, CancellationToken cancellationToken);
    
    Task<UpdateStoreCustomerResponse> UpdateStoreCustomerAsync(UpdateStoreCustomerCommand command, CancellationToken cancellationToken);
}


public partial class PosService : IPosService
{
    public async Task<GetStoreCustomersResponse> GetStoreCustomersAsync(GetStoreCustomersRequest request, CancellationToken cancellationToken)
    {
        var (count, customers) = await _posDataProvider.GetStoreCustomersAsync(request.StoreId, request.PageIndex, request.PageSize, request.Phone, cancellationToken).ConfigureAwait(false);

        return new GetStoreCustomersResponse
        {
            Data = new GetPosCustomerInfoResponseData
            {
                Count = count,
                Customers = _mapper.Map<List<StoreCustomerDto>>(customers)
            }
        };
    }

    public async Task<UpdateStoreCustomerResponse> UpdateStoreCustomerAsync(UpdateStoreCustomerCommand command, CancellationToken cancellationToken)
    {
        var customer = await _posDataProvider.GetStoreCustomerAsync(id: command.CustomerId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (customer == null) throw new Exception("Customer not found");
        
        _mapper.Map(command, customer);
        
        await _posDataProvider.UpdateStoreCustomersAsync([customer], cancellationToken: cancellationToken).ConfigureAwait(false);

        return new UpdateStoreCustomerResponse
        {
            Data = _mapper.Map<StoreCustomerDto>(customer)
        };
    }
}