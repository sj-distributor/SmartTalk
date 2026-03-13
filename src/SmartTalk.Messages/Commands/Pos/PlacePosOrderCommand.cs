using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Enums.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class PlacePosOrderCommand : ICommand
{
    public int? OrderId { get; set; }
    
    public int StoreId { get; set; }
    
    public string OrderItems { get; set; }

    public bool IsWithRetry { get; set; } = true;
    
    public string Phone { get; set; }
    
    public string Name { get; set; }
    
    public string Address { get; set; }
    
    public string Latitude { get; set; }
    
    public string Longitude { get; set; }
    
    public string Room { get; set; }
    
    public string Remarks { get; set; }
    
    public string Notes { get; set; }
    
    public decimal Tax { get; set; }
    
    public decimal Total { get; set; }
    
    public decimal SubTotal { get; set; }
    
    public PosOrderReceiveType Type { get; set; }
    
    public int Count { get; set; }

    public bool IsPersistAction { get; set; } = false;
}

public class PlacePosOrderResponse : SmartTalkResponse<PosOrderDto>;