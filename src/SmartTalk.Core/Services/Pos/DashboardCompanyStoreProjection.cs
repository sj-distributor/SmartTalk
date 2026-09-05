using SmartTalk.Messages.Requests.Pos;

namespace SmartTalk.Core.Services.Pos;

public class DashboardCompanyStoreProjection
{
    public DataDashBoardCompanyOptionDto Company { get; set; }
    
    public DataDashBoardStoreOptionDto Store { get; set; }
}
