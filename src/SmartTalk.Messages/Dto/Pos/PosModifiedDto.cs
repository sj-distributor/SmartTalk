using SmartTalk.Messages.Dto.EasyPos;
using SmartTalk.Messages.Enums.Pos;

namespace SmartTalk.Messages.Dto.Pos;

public class PosModifiedDto
{
    public PosModifyType ModifyType { get; set; }
    
    public List<PosModifiedContentDto> Contents { get; set; }
}

public class PosModifiedContentDto
{
    public object Content { get; set; }
    
    public PosMenuContentType ContentType { get; set; }
}

public class PosMenuModifiedDto
{
    public List<EasyPosResponseMenu> Menus { get; set; }
}

public class PosCategoryModifiedDto
{
    public long MenuId { get; set; }
    
    public List<EasyPosResponseCategory> Categories { get; set; }
}

public class PosProductModifiedDto
{
    public long MenuId { get; set; }
    
    public long CategoryId { get; set; }
    
    public List<EasyPosResponseProduct> Products { get; set; }
}