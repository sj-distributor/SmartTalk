using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService
{
    Task<GetAiSpeechAssistantDynamicConfigsResponse> GetAiSpeechAssistantDynamicConfigsAsync(GetAiSpeechAssistantDynamicConfigsRequest request,
        CancellationToken cancellationToken);

    Task<UpdateAiSpeechAssistantDynamicConfigResponse> UpdateAiSpeechAssistantDynamicConfigAsync(UpdateAiSpeechAssistantDynamicConfigCommand command,
        CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantService
{
    public async Task<GetAiSpeechAssistantDynamicConfigsResponse> GetAiSpeechAssistantDynamicConfigsAsync(GetAiSpeechAssistantDynamicConfigsRequest request,
        CancellationToken cancellationToken)
    {
        var configs = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantDynamicConfigsAsync(request.Status, cancellationToken).ConfigureAwait(false);
        var (_, companies) = await _posDataProvider.GetPosCompaniesAsync(isBindConfig: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        var dtoMap = _mapper.Map<List<AiSpeechAssistantDynamicConfigDto>>(configs).ToDictionary(x => x.Id);

        foreach (var dto in dtoMap.Values)
            dto.Children = [];

        foreach (var config in configs)
        {
            if (!config.ParentId.HasValue) continue;
            if (!dtoMap.TryGetValue(config.ParentId.Value, out var parent)) continue;
            parent.Children.Add(dtoMap[config.Id]);
        }

        List<AiSpeechAssistantDynamicConfigDto> roots;
        if (request.Id.HasValue)
        {
            roots = dtoMap.TryGetValue(request.Id.Value, out var node) ? [node] : [];
        }
        else
        {
            roots = configs
                .Where(x => !x.ParentId.HasValue || !dtoMap.ContainsKey(x.ParentId.Value))
                .Select(x => dtoMap[x.Id])
                .ToList();
        }

        return new GetAiSpeechAssistantDynamicConfigsResponse
        {
            Data = new GetAiSpeechAssistantDynamicConfigsResponseData
            {
                Configs = roots,
                Companies = _mapper.Map<List<CompanyDto>>(companies)
            }
        };
    }

    public async Task<UpdateAiSpeechAssistantDynamicConfigResponse> UpdateAiSpeechAssistantDynamicConfigAsync(UpdateAiSpeechAssistantDynamicConfigCommand command,
        CancellationToken cancellationToken)
    {
        var config = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantDynamicConfigByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (config == null) throw new Exception($"Could not found dynamic config, id={command.Id}");
        
        config.Status = command.Status;

        var updated = await _aiSpeechAssistantDataProvider.UpdateAiSpeechAssistantDynamicConfigAsync(config, false, cancellationToken).ConfigureAwait(false);

        var selectedCompanyIds = command.CompanyIds?.Distinct().ToHashSet() ?? [];
        var (_, allCompanies) = await _posDataProvider.GetPosCompaniesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        foreach (var company in allCompanies)
            company.IsBindConfig = selectedCompanyIds.Contains(company.Id);

        await _posDataProvider.UpdatePosCompaniesAsync(allCompanies, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new UpdateAiSpeechAssistantDynamicConfigResponse
        {
            Data = _mapper.Map<AiSpeechAssistantDynamicConfigDto>(updated)
        };
    }
}
