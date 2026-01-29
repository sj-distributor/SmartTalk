using SmartTalk.Messages.Enums.DynamicInterface;

namespace SmartTalk.Messages.Dto.DynamicInterface;

public class DynamicInterfaceTreeNodeDto
{
    public int Id { get; set; }

    public string Name { get; set; }

    public VariableLevelType LevelType { get; set; }

    public bool IsEnabled { get; set; }

    public List<DynamicInterfaceTreeNodeDto> Children { get; set; }
}