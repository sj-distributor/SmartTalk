using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.VoiceAi.PosManagement;

public class GetPosMenuPreviewRequest : IRequest
{
    public string ProductName { get; set; }
    
    public int StoreId { get; set; }
}

public class GetPosMenuPreviewResponse : SmartTalkResponse<PosMenuPreviewData>
{
    
}

public class PosMenuPreviewData
{
    public List<PosMenuWithCategories> MenuWithCategories { get; set; }
}

public class PosMenuWithCategories
{
    public PosMenuDto Menu { get; set; }
    
    public List<PosCategoryWithProduct> PosCategoryWithProduct { get; set; }
}

public class PosCategoryWithProduct
{
    public PosCategoryDto Category { get; set; }
    
    public List<PosProductDto> Products { get; set; }
}