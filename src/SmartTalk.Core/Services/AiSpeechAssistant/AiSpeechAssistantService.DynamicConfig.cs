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

        var categoryConfigIds = configs
            .Where(x => x.Level == AiSpeechAssistantDynamicConfigLevel.Category)
            .Select(x => x.Id)
            .ToList();

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

    public async Task<GetCurrentCompanyDynamicConfigsResponse> GetCurrentCompanyDynamicConfigsAsync(GetCurrentCompanyDynamicConfigsRequest request,
        CancellationToken cancellationToken)
    {
        var store = await _posDataProvider.GetPosCompanyStoreAsync(id: request.StoreId, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (store == null)
            return new GetCurrentCompanyDynamicConfigsResponse
            {
                Data = new GetCurrentCompanyDynamicConfigsResponseData { Configs = [] }
            };

        var company = await _posDataProvider.GetPosCompanyAsync(store.CompanyId, cancellationToken).ConfigureAwait(false);
        
        var configRelatingCompany = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantDynamicConfigRelatingCompaniesAsync(companyIds: [company.Id], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        if (configRelatingCompany == null)
            return new GetCurrentCompanyDynamicConfigsResponse
            {
                Data = new GetCurrentCompanyDynamicConfigsResponseData
                {
                    Store = _mapper.Map<CompanyStoreDto>(store),
                    Configs = []
                }
            };

        var activeConfigs = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantDynamicConfigsAsync(cancellationToken).ConfigureAwait(false);
        var roots = BuildDynamicConfigTree(activeConfigs, true);

        return new GetCurrentCompanyDynamicConfigsResponse
        {
            Data = new GetCurrentCompanyDynamicConfigsResponseData
            {
                Store = _mapper.Map<CompanyStoreDto>(store),
                Configs = roots
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

    private List<AiSpeechAssistantDynamicConfigDto> BuildDynamicConfigTree(List<AiSpeechAssistantDynamicConfig> configs, bool isFilter = false)
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

        if (!isFilter)
            return roots;

        return roots
            .Where(x => x.Status)
            .Select(FilterByStatus)
            .ToList();
    }

    private AiSpeechAssistantDynamicConfigDto FilterByStatus(AiSpeechAssistantDynamicConfigDto node)
    {
        node.Children = node.Children
            .Where(x => x.Status)
            .Select(FilterByStatus)
            .ToList();

        return node;
    }
}
