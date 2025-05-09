using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.VoiceAi.PosManagement;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.VoiceAi.PosManagement;

public class GetPosMenuPrviewRequest : IRequest
{
    public int MenuId { get; set; }
}

public class GetPosMenuPreviewResponse : SmartTalkResponse<PosMenuPreviewData>
{
    
}

public class PosMenuPreviewData
{
    public List<PosCategoryWithProduct> CategoryWithProduct { get; set; }
}

public class PosCategoryWithProduct
{
    public PosCategoryDto Category { get; set; }
    
    public List<PosProductDto> Products { get; set; }
}