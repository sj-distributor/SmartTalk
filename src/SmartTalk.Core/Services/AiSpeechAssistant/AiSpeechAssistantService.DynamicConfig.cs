using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService
{
    Task<GetAiSpeechAssistantDynamicConfigsResponse> GetAiSpeechAssistantDynamicConfigsAsync(GetAiSpeechAssistantDynamicConfigsRequest request,
        CancellationToken cancellationToken);

    Task<GetCurrentCompanyDynamicConfigsResponse> GetCurrentCompanyDynamicConfigsAsync(GetCurrentCompanyDynamicConfigsRequest request,
        CancellationToken cancellationToken);

    Task<UpdateAiSpeechAssistantDynamicConfigResponse> UpdateAiSpeechAssistantDynamicConfigAsync(UpdateAiSpeechAssistantDynamicConfigCommand command,
        CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantService
{
    public async Task<GetAiSpeechAssistantDynamicConfigsResponse> GetAiSpeechAssistantDynamicConfigsAsync(GetAiSpeechAssistantDynamicConfigsRequest request, CancellationToken cancellationToken)
    {
        var configs = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantDynamicConfigsAsync(cancellationToken)
            .ConfigureAwait(false);

        var roots = BuildDynamicConfigTree(configs);

        var categoryConfigIds = new List<int>();
        var stack = new Stack<AiSpeechAssistantDynamicConfigDto>(roots);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node.Level == AiSpeechAssistantDynamicConfigLevel.Category)
                categoryConfigIds.Add(node.Id);

            foreach (var child in node.Children)
                stack.Push(child);
        }

        if (categoryConfigIds.Count > 0)
        {
            var relatedCompanies = await _aiSpeechAssistantDataProvider
                .GetAiSpeechAssistantDynamicConfigRelatingCompaniesAsync(categoryConfigIds, null, cancellationToken)
                .ConfigureAwait(false);

            var companyLookup = relatedCompanies
                .GroupBy(x => x.ConfigId)
                .ToDictionary(
                    x => x.Key,
                    x => x.Select(c => new CompanyDto
                    {
                        Id = c.CompanyId,
                        Name = c.CompanyName
                    }).ToList());

            FillCompanies(roots, companyLookup);
        }

        return new GetAiSpeechAssistantDynamicConfigsResponse
        {
            Data = new GetAiSpeechAssistantDynamicConfigsResponseData
            {
                Configs = roots
            }
        };
    }
    
    private void FillCompanies(List<AiSpeechAssistantDynamicConfigDto> nodes, Dictionary<int, List<CompanyDto>> companyLookup)
    {
        foreach (var node in nodes)
        {
            if (node.Level == AiSpeechAssistantDynamicConfigLevel.Category &&
                companyLookup.TryGetValue(node.Id, out var companies))
            {
                node.Companies = companies;
            }

            if (node.Children.Count > 0)
            {
                FillCompanies(node.Children, companyLookup);
            }
        }
    }

    public async Task<GetCurrentCompanyDynamicConfigsResponse> GetCurrentCompanyDynamicConfigsAsync(GetCurrentCompanyDynamicConfigsRequest request, CancellationToken cancellationToken)
    {
        static GetCurrentCompanyDynamicConfigsResponse BuildEmptyResponse() => new()
        {
            Data = new GetCurrentCompanyDynamicConfigsResponseData
            {
                Store = null,
                Configs = []
            }
        };

        var company = await _posDataProvider.GetPosCompanyByStoreIdAsync(request.StoreId, cancellationToken).ConfigureAwait(false);
        if (company == null)
            return BuildEmptyResponse();

        var configRelatingCompany = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantDynamicConfigRelatingCompaniesAsync(companyIds: [company.Id], cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var relatedConfigIds = configRelatingCompany
            .Select(x => x.ConfigId)
            .Distinct()
            .ToHashSet();
        
        if (relatedConfigIds.Count == 0)
            return BuildEmptyResponse();

        var allConfigs = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantDynamicConfigsAsync(cancellationToken).ConfigureAwait(false);

        var relatedCategoryConfigIds = allConfigs
            .Where(x => x.Level == AiSpeechAssistantDynamicConfigLevel.Category &&
                        x.Status &&
                        relatedConfigIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToHashSet();
        if (relatedCategoryConfigIds.Count == 0)
            return BuildEmptyResponse();

        var store = await _posDataProvider
            .GetPosCompanyStoreAsync(id: request.StoreId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        
        if (store == null)
            return BuildEmptyResponse();

        var roots = BuildDynamicConfigTree(allConfigs);

        AiSpeechAssistantDynamicConfigDto? FilterNode(AiSpeechAssistantDynamicConfigDto node, bool isRoot, bool inRelatedCategoryBranch)
        {
            if (!node.Status)
                return null;

            var isRelatedCategory = node.Level == AiSpeechAssistantDynamicConfigLevel.Category &&
                                    relatedCategoryConfigIds.Contains(node.Id);
            var nextInRelatedCategoryBranch = inRelatedCategoryBranch || isRelatedCategory;

            if (node.Level == AiSpeechAssistantDynamicConfigLevel.Category &&
                !nextInRelatedCategoryBranch)
            {
                return null;
            }

            if (node.Children.Count > 0)
            {
                node.Children = node.Children
                    .Select(child => FilterNode(child, false, nextInRelatedCategoryBranch))
                    .Where(x => x != null)
                    .Select(x => x!)
                    .ToList();
            }

            if (node.Level == AiSpeechAssistantDynamicConfigLevel.Category &&
                node.Children.Count == 0)
            {
                return null;
            }

            if (!isRoot &&
                !nextInRelatedCategoryBranch &&
                node.Children.Count == 0)
            {
                return null;
            }

            if (isRoot &&
                node.Level == AiSpeechAssistantDynamicConfigLevel.System &&
                node.Children.Count == 0)
            {
                return null;
            }

            return node;
        }

        var filteredRoots = roots
            .Where(root => root.Level == AiSpeechAssistantDynamicConfigLevel.System)
            .Select(root =>
            {
                var filtered = FilterNode(root, true, false);
                if (filtered == null)
                    return null;

                return filtered;
            })
            .Where(x => x != null)
            .Select(x => x!)
            .ToList();

        return new GetCurrentCompanyDynamicConfigsResponse
        {
            Data = new GetCurrentCompanyDynamicConfigsResponseData
            {
                Store = _mapper.Map<CompanyStoreDto>(store),
                Configs = filteredRoots
            }
        };
    }

    public async Task<UpdateAiSpeechAssistantDynamicConfigResponse> UpdateAiSpeechAssistantDynamicConfigAsync(UpdateAiSpeechAssistantDynamicConfigCommand command, CancellationToken cancellationToken)
    {
        var config = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantDynamicConfigByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        
        if (config == null) throw new Exception($"Could not found dynamic config, id={command.Id}");

        config.Status = command.Status;
        
        var updated = await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantDynamicConfigAsync(config, false, cancellationToken).ConfigureAwait(false);
        
        var selectedCompanyIds = command.CompanyIds?.Distinct().ToList() ?? [];

        if (selectedCompanyIds.Count > 0)
        {
            var currentRelations = await _aiSpeechAssistantDataProvider
                .GetAiSpeechAssistantDynamicConfigRelatingCompaniesAsync([config.Id], cancellationToken: cancellationToken).ConfigureAwait(false);

            if (currentRelations.Count > 0)
            {
                await _aiSpeechAssistantDataProvider
                    .DeleteAiSpeechAssistantDynamicConfigRelatingCompaniesAsync(currentRelations, true, cancellationToken).ConfigureAwait(false);
            }
            
            var (_, companies) = await _posDataProvider
                .GetPosCompaniesAsync(companyIds: selectedCompanyIds, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var companyLookup = companies.ToDictionary(x => x.Id, x => x.Name);

            var newRelations = selectedCompanyIds
                .Where(companyId => companyLookup.ContainsKey(companyId))
                .Select(companyId => new AiSpeechAssistantDynamicConfigRelatingCompany
                {
                    ConfigId = config.Id,
                    CompanyId = companyId,
                    CompanyName = companyLookup[companyId]
                })
                .ToList();

            if (newRelations.Count > 0)
            {
                await _aiSpeechAssistantDataProvider
                    .AddAiSpeechAssistantDynamicConfigRelatingCompaniesAsync(newRelations, true, cancellationToken).ConfigureAwait(false);
            }
        }

        return new UpdateAiSpeechAssistantDynamicConfigResponse
        {
            Data = _mapper.Map<AiSpeechAssistantDynamicConfigDto>(updated)
        };
    }

    private List<AiSpeechAssistantDynamicConfigDto> BuildDynamicConfigTree(List<AiSpeechAssistantDynamicConfig> configs)
    {
        var dtoMap = _mapper.Map<List<AiSpeechAssistantDynamicConfigDto>>(configs)
            .ToDictionary(x => x.Id);

        foreach (var dto in dtoMap.Values)
            dto.Children = [];

        foreach (var config in configs)
        {
            if (!config.ParentId.HasValue) continue;
            if (!dtoMap.TryGetValue(config.ParentId.Value, out var parent)) continue;

            parent.Children.Add(dtoMap[config.Id]);
        }

        var roots = configs
            .Where(x => !x.ParentId.HasValue || !dtoMap.ContainsKey(x.ParentId.Value))
            .Select(x => dtoMap[x.Id])
            .ToList();

        return roots;
    }

    private AiSpeechAssistantDynamicConfigDto FilterByStatus(AiSpeechAssistantDynamicConfigDto node, HashSet<int> parentIds)
    { 
        if (!node.Status)
            return null;

        node.Children = node.Children
            .Select(child => FilterByStatus(child, parentIds))
            .Where(x => x != null)
            .Select(x => x!)
            .ToList();

        var hadChildren = parentIds.Contains(node.Id);

        if (hadChildren && node.Children.Count == 0)
            return null;

        return node;
    }
    
    private List<AiSpeechAssistantDynamicConfigDto> FilterDynamicConfigTree(List<AiSpeechAssistantDynamicConfigDto> roots, List<AiSpeechAssistantDynamicConfig> originalConfigs)
    {
        var parentIds = originalConfigs
            .Where(x => x.ParentId.HasValue)
            .Select(x => x.ParentId!.Value)
            .ToHashSet();

        return roots
            .Select(x => FilterByStatus(x, parentIds))
            .Where(x => x != null)
            .Select(x => x!)
            .ToList();
    }
}
