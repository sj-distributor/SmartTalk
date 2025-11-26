using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

public class GetPhoneOrderDataDashboardRequest : IRequest
{
    public DateTimeOffset StartDate { get; set; }
    
    public DateTimeOffset EndDate { get; set; }
    
    public PhoneOrderDataDashDataType DataType { get; set; }
    
    public List<int> StoreIds { get; set; }
    
    public List<int> AgentIds { get; set; }

    public int? InvalidCallSeconds { get; set; } = 10;
}

public class GetPhoneOrderDataDashboardResponse : SmartTalkResponse<GetPhoneOrderDataDashboardResponseData>
{
}

public class GetPhoneOrderDataDashboardResponseData
{
    public CallInDataDto CallInData { get; set; }
    
    public CallOutDataDto CallOutData { get; set; }
    
    public RestaurantDataDto Restaurant { get; set; }
}

public class CallInDataDto
{
    public int AnsweredCallInCount { get; set; }
    
    public int CountChange { get; set; }
    
    public int CallInAnsweredByHumanCount { get; set; }

    public double AverageCallInDurationSeconds { get; set; }

    public int EffectiveCommunicationCallInCount { get; set; } 

    public double RepeatCallInRate { get; set; }

    public double CallInSatisfactionRate { get; set; }

    public int CallInMissedByHumanCount { get; set; }

    public double CallinTransferToHumanRate { get; set; }

    public double TotalCallInDurationSeconds { get; set; } 
    
    public Dictionary<string, double> TotalCallInDurationPerPeriod { get; set; } = new();
    
}

public class CallOutDataDto
{
    public int AnsweredCallOutCount { get; set; }

    public int CountChange { get; set; }
    
    public double AverageCallOutDurationSeconds { get; set; }

    public int CallOutNotAnsweredCount { get; set; } 

    public int CallOutAnsweredByHumanCount { get; set; }

    public int EffectiveCommunicationCallOutCount { get; set; } 

    public double CallOutSatisfactionRate { get; set; }

    public double TotalCallOutDurationSeconds { get; set; }
    
    public Dictionary<string, double> TotalCallOutDurationPerPeriod { get; set; } = new();
}

public class RestaurantDataDto
{
    public int OrderCount { get; set; }

    public decimal TotalOrderAmount { get; set; }
    
    public int OrderCountChange { get; set; }
    
    public decimal OrderAmountChange { get; set; }
    
    public int CancelledOrderCount { get; set; }
    
    public Dictionary<string,int> OrderCountPerPeriod { get; set; } = new();
    
    public Dictionary<string,int> CancelledOrderCountPerPeriod { get; set; } = new();
}