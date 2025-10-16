using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

public class GetPhoneOrderDataDashboardRequest : IRequest
{
    public DateTimeOffset? startDate { get; set; }
    
    public DateTimeOffset? endDate { get; set; }
    
    public List<int> storeIds { get; set; }
    
    public List<int> agentIds { get; set; }

    public int? invalidCallSeconds { get; set; } = 3;
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

    public double AverageCallInDurationSeconds { get; set; }

    public int EffectiveCommunicationCallInCount { get; set; } 

    public double RepeatCallInRate { get; set; }

    public double CallInSatisfactionRate { get; set; }

    public int CallInMissedByHumanCount { get; set; }

    public double CallinTransferToHumanRate { get; set; }

    public double TotalCallInDurationSeconds { get; set; } 
    
}

public class CallOutDataDto
{
    public int AnsweredCallOutCount { get; set; }

    public double AverageCallOutDurationSeconds { get; set; }

    public int CallOutNotAnsweredCount { get; set; } 

    public int CallOutAnsweredByHumanCount { get; set; }

    public int EffectiveCommunicationCallOutCount { get; set; } 

    public double CallOutSatisfactionRate { get; set; }

    public double TotalCallOutDurationSeconds { get; set; }
}

public class RestaurantDataDto
{
    public int OrderCount { get; set; }

    public decimal TotalOrderAmount { get; set; }
    
    public int CancelledOrderCount { get; set; }
}