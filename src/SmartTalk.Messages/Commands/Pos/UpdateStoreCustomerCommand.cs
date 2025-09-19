using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class UpdateStoreCustomerCommand : ICommand
{
    public int CustomerId { get; set; }
    
    public string Name { get; set; }

    public string Address { get; set; }

    public string Latitude { get; set; }

    public string Longitude { get; set; }
    
    public string Room { get; set; }

    public string Remarks { get; set; }
    
    public bool IsDeleted { get; set; }
}

public class UpdateStoreCustomerResponse : SmartTalkResponse<StoreCustomerDto>;