using Mediator.Net.Contracts;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

[SmartTalkAuthorize(Permissions = new[] { SecurityStore.Permissions.CanViewDataDashboard })]
public class GetDataDashBoardOptionsRequest : HasServiceProviderId, IRequest
{
}

public class GetDataDashBoardOptionsResponse : SmartTalkResponse<GetDataDashBoardOptionsResponseData>;

public class GetDataDashBoardOptionsResponseData
{
    public List<DataDashBoardCompanyWithStoresOptionDto> Companies { get; set; }

    public List<DataDashBoardStoreAgentsOptionDto> StoresAgents { get; set; }

    public List<int> AllStoreIds { get; set; }

    public List<int> AllAgentIds { get; set; }
}

public class DataDashBoardCompanyWithStoresOptionDto
{
    public int Count { get; set; }
    
    public DataDashBoardCompanyOptionDto Company { get; set; }
    
    public List<DataDashBoardStoreOptionDto> Stores { get; set; }
}

public class DataDashBoardCompanyOptionDto
{
    public int Id { get; set; }
    
    public string Name { get; set; }
}

public class DataDashBoardStoreAgentsOptionDto
{
    public DataDashBoardStoreOptionDto Store { get; set; }
    
    public List<AgentDetailDto> Agents { get; set; }
}

public class DataDashBoardStoreOptionDto
{
    public int Id { get; set; }
    
    public int CompanyId { get; set; }
    
    public string Names { get; set; }
}
