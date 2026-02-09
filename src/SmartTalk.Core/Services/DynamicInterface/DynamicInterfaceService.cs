using AutoMapper;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.DynamicInterface;
using SmartTalk.Messages.Enums.DynamicInterface;
using SmartTalk.Messages.Requests.DynamicInterface;

namespace SmartTalk.Core.Services.DynamicInterface;

public interface IDynamicInterfaceService : IScopedDependency
{
    Task<GetDynamicInterfaceTreeResponse> GetDynamicInterfaceTreeAsync(GetDynamicInterfaceTreeRequest request, CancellationToken cancellationToken);
}

public class DynamicInterfaceService : IDynamicInterfaceService
{
    private readonly IMapper _mapper;
    private readonly IDynamicInterfaceDataProvider _dynamicInterfaceDataProvider;

    public DynamicInterfaceService(IMapper mapper, IDynamicInterfaceDataProvider dynamicInterfaceDataProvider)
    {
        _mapper = mapper;
        _dynamicInterfaceDataProvider = dynamicInterfaceDataProvider;
    }

    public async Task<GetDynamicInterfaceTreeResponse> GetDynamicInterfaceTreeAsync(GetDynamicInterfaceTreeRequest request, CancellationToken cancellationToken)
    {
        var allNodes = await _dynamicInterfaceDataProvider.GetDynamicInterfaceNodesAsync(cancellationToken).ConfigureAwait(false);

        var filteredNodes = FilterWithAncestors(allNodes, request.Keyword);

        var tree = BuildTree(filteredNodes);

        return new GetDynamicInterfaceTreeResponse
        {
            Data = new GetDynamicInterfaceTreeResponseData
            {
                TreeNodes = tree
            }
        };
    }

    
    private static List<AiSpeechAssistantKnowledgeVariableCache> FilterWithAncestors(List<AiSpeechAssistantKnowledgeVariableCache> allNodes, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return allNodes;
        }

        var nodeMap = allNodes.ToDictionary(x => x.Id);
        var result = new Dictionary<int, AiSpeechAssistantKnowledgeVariableCache>();

        bool IsMatch(AiSpeechAssistantKnowledgeVariableCache x) =>
            (x.SystemName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (x.CategoryName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (x.FieldName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);

        foreach (var node in allNodes.Where(IsMatch))
        {
            var current = node;

            while (current != null)
            {
                if (result.ContainsKey(current.Id))
                {
                    break;
                }

                result[current.Id] = current;

                if (current.ParentId.HasValue &&
                    nodeMap.TryGetValue(current.ParentId.Value, out var parent))
                {
                    current = parent;
                }
                else
                {
                    break;
                }
            }
        }

        return result.Values.ToList();
    }


    private static List<DynamicInterfaceTreeNodeDto> BuildTree(List<AiSpeechAssistantKnowledgeVariableCache> variableNodes)
    {
        if (variableNodes == null || variableNodes.Count == 0)
        {
            return new List<DynamicInterfaceTreeNodeDto>();
        }

        var orderedNodes = variableNodes.OrderBy(x => x.LevelType).ThenBy(x => x.Id).ToList();
        
        var nodeMap = orderedNodes.ToDictionary(x => x.Id, 
            x => new DynamicInterfaceTreeNodeDto 
            {
                Id = x.Id,
                Name = GetNodeName(x),
                LevelType = x.LevelType,
                IsEnabled = x.IsEnabled,
                Children = new List<DynamicInterfaceTreeNodeDto>()
            });

        var roots = new List<DynamicInterfaceTreeNodeDto>();

        foreach (var node in orderedNodes)
        {
            var current = nodeMap[node.Id];

            if (node.ParentId.HasValue && nodeMap.TryGetValue(node.ParentId.Value, out var parent))
            {
                parent.Children.Add(current);
            }
            else
            {
                roots.Add(current);
            }
        } 
        SortChildrenRecursively(roots);
        
        return roots;
    }
    
    private static string GetNodeName(AiSpeechAssistantKnowledgeVariableCache node)
    {
        return node.LevelType switch
        {
            VariableLevelType.System => node.SystemName ?? string.Empty,
            VariableLevelType.Category => node.CategoryName ?? string.Empty,
            VariableLevelType.Field => node.FieldName ?? string.Empty,
            _ => string.Empty
        };
    }
    
    private static void SortChildrenRecursively(List<DynamicInterfaceTreeNodeDto> nodes)
    {
        nodes.Sort((a, b) =>
        {
            var levelCompare = a.LevelType.CompareTo(b.LevelType);
            return levelCompare != 0 ? levelCompare : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });

        foreach (var node in nodes)
        {
            if (node.Children?.Any() == true)
            {
                SortChildrenRecursively(node.Children);
            }
        }
    }
}