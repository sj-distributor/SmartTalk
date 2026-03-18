using DocumentFormat.OpenXml.Office2010.ExcelAc;
using Serilog;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService
{
    Task<GetNumbersResponse> GetNumbersAsync(GetNumbersRequest request, CancellationToken cancellationToken);

    Task<GetAiSpeechAssistantsResponse> GetAiSpeechAssistantsAsync(GetAiSpeechAssistantsRequest request, CancellationToken cancellationToken);

    Task<GetAiSpeechAssistantKnowledgeResponse> GetAiSpeechAssistantKnowledgeAsync(GetAiSpeechAssistantKnowledgeRequest request, CancellationToken cancellationToken);
    
    Task<GetAiSpeechAssistantKnowledgeHistoryResponse> GetAiSpeechAssistantKnowledgeHistoryAsync(GetAiSpeechAssistantKnowledgeHistoryRequest request, CancellationToken cancellationToken);

    Task<GetAiSpeechAssistantByIdResponse> GetAiSpeechAssistantByIdAsync(GetAiSpeechAssistantByIdRequest request, CancellationToken cancellationToken);
    
    Task<GetAiSpeechAssistantSessionResponse> GetAiSpeechAssistantSessionAsync(GetAiSpeechAssistantSessionRequest request, CancellationToken cancellationToken);
    
    Task<GetAiSpeechAssistantInboundRoutesResponse> GetAiSpeechAssistantInboundRoutesAsync(GetAiSpeechAssistantInboundRoutesRequest request, CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantService
{
    public async Task<GetNumbersResponse> GetNumbersAsync(GetNumbersRequest request, CancellationToken cancellationToken)
    {
        var (count, numbers) = await _aiSpeechAssistantDataProvider.GetNumbersAsync(
            request.PageIndex, request.PageSize, cancellationToken).ConfigureAwait(false);

        return new GetNumbersResponse
        {
            Data = new GetNumbersResponseData
            {
                Count = count,
                Numbers = _mapper.Map<List<NumberPoolDto>>(numbers)
            }
        };
    }

    public async Task<GetAiSpeechAssistantsResponse> GetAiSpeechAssistantsAsync(GetAiSpeechAssistantsRequest request, CancellationToken cancellationToken)
    {
        var agentIds = request.AgentId.HasValue
            ? [request.AgentId.Value]
            : request.StoreId.HasValue
                ? (await _posDataProvider.GetPosAgentsAsync(storeIds: [request.StoreId.Value], cancellationToken: cancellationToken).ConfigureAwait(false)).Select(x => x.AgentId).ToList()
                : [];

        Log.Information("Get the agent ids: {@AgentIds}", agentIds);

        if (agentIds.Count == 0)
        {
            return new GetAiSpeechAssistantsResponse()
            {
                Data = new GetAiSpeechAssistantsResponseData()
                {
                    Count = 0,
                    Assistants = new List<AiSpeechAssistantDto>()
                }
            };
        }

        var (count, assistants) = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantsAsync(
            request.PageIndex, request.PageSize, request.Channel.HasValue ? request.Channel.Value.ToString("D") : string.Empty, request.Keyword, agentIds, request.IsDefault, cancellationToken).ConfigureAwait(false);

        var enrichAssistants = _mapper.Map<List<AiSpeechAssistantDto>>(assistants);
        await EnrichAssistantsInfoAsync(enrichAssistants, cancellationToken).ConfigureAwait(false);

        return new GetAiSpeechAssistantsResponse
        {
            Data = new GetAiSpeechAssistantsResponseData
            {
                Count = count,
                Assistants = enrichAssistants,
                AnsweringNumber = enrichAssistants.Where(x => x.IsDefault).FirstOrDefault()?.AnsweringNumber ?? string.Empty
            }
        };
    }

    public async Task<GetAiSpeechAssistantKnowledgeResponse> GetAiSpeechAssistantKnowledgeAsync(GetAiSpeechAssistantKnowledgeRequest request, CancellationToken cancellationToken)
    {
        var knowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(request.AssistantId, request.KnowledgeId, request.KnowledgeId.HasValue ? null : true, cancellationToken).ConfigureAwait(false);

        if (knowledge == null) { return new GetAiSpeechAssistantKnowledgeResponse { Data = null }; }

        var result = _mapper.Map<AiSpeechAssistantKnowledgeDto>(knowledge);
        var premise = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantPremiseByAssistantIdAsync(request.AssistantId, cancellationToken).ConfigureAwait(false);
        
        if (premise != null && !string.IsNullOrEmpty(premise.Content))
            result.Premise = _mapper.Map<AiSpeechAssistantPremiseDto>(premise);
        
        var details = await _aiSpeechAssistantDataProvider.GetKnowledgeDetailsByKnowledgeIdAsync(knowledge.Id, cancellationToken).ConfigureAwait(false);

        var detailDtos = _mapper.Map<List<AiSpeechAssistantKnowledgeDetailDto>>(details);
        foreach (var dto in detailDtos)
            dto.RelatedKnowledgeId = knowledge.Id;

        var allCopyRelateds = await _aiSpeechAssistantDataProvider.GetKnowledgeCopyRelatedByTargetKnowledgeIdAsync([knowledge.Id], null,  cancellationToken).ConfigureAwait(false);
        
        Log.Information("Get the knowledge copy related Ids: {@Ids}", allCopyRelateds.Select(x => x.Id));

        result.KnowledgeCopyRelateds = await EnhanceRelateFrom(allCopyRelateds, cancellationToken).ConfigureAwait(false);

        if (allCopyRelateds is { Count: > 0 })
        {
            var sourceKnowledgeIds = allCopyRelateds.Select(x => x.SourceKnowledgeId).Distinct().ToList();
            var sourceKnowledges = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgesAsync(sourceKnowledgeIds, cancellationToken).ConfigureAwait(false);
            var sourceDetailTasks = sourceKnowledgeIds.ToDictionary(
                id => id,
                id => _aiSpeechAssistantDataProvider.GetKnowledgeDetailsByKnowledgeIdAsync(id, cancellationToken));

            await Task.WhenAll(sourceDetailTasks.Values).ConfigureAwait(false);

            var relatedFromMap = result.KnowledgeCopyRelateds?
                .GroupBy(x => x.SourceKnowledgeId)
                .ToDictionary(g => g.Key, g => g.FirstOrDefault()?.RelatedFrom)
                ?? new Dictionary<int, string>();

            var sourceDetailSignatureLookup = new Dictionary<string, (int SourceKnowledgeId, string RelatedFrom)>();
            var existingDetailSignatures = detailDtos
                .Select(x => BuildDetailSignature(x.KnowledgeName, x.FormatType, x.Content, x.FileName))
                .ToHashSet();

            foreach (var sourceKnowledge in sourceKnowledges)
            {
                if (!sourceDetailTasks.TryGetValue(sourceKnowledge.Id, out var sourceDetailTask))
                    continue;

                var sourceDetails = sourceDetailTask.Result;
                if (sourceDetails == null || sourceDetails.Count == 0)
                    continue;

                relatedFromMap.TryGetValue(sourceKnowledge.Id, out var relatedFrom);

                foreach (var sourceDetail in sourceDetails)
                {
                    var copiedName = EnsureCopySuffixForDetailMatching(sourceDetail.KnowledgeName);
                    var signature = BuildDetailSignature(copiedName, sourceDetail.FormatType, sourceDetail.Content, sourceDetail.FileName);
                    sourceDetailSignatureLookup.TryAdd(signature, (sourceKnowledge.Id, relatedFrom));

                    if (existingDetailSignatures.Contains(signature))
                        continue;

                    detailDtos.Add(new AiSpeechAssistantKnowledgeDetailDto
                    {
                        Id = sourceDetail.Id,
                        KnowledgeId = sourceDetail.KnowledgeId,
                        KnowledgeName = copiedName,
                        FormatType = sourceDetail.FormatType,
                        Content = sourceDetail.Content,
                        FileName = sourceDetail.FileName,
                        CreatedDate = sourceDetail.CreatedDate,
                        LastModifiedBy = sourceDetail.LastModifiedBy,
                        LastModifiedDate = sourceDetail.LastModifiedDate,
                        RelatedKnowledgeId = sourceKnowledge.Id,
                        RelatedFrom = relatedFrom
                    });
                    existingDetailSignatures.Add(signature);
                }
            }

            foreach (var dto in detailDtos.Where(x => !string.IsNullOrWhiteSpace(x.KnowledgeName) &&
                                                      x.KnowledgeName.EndsWith("-副本", StringComparison.Ordinal)))
            {
                var signature = BuildDetailSignature(dto.KnowledgeName, dto.FormatType, dto.Content, dto.FileName);
                if (!sourceDetailSignatureLookup.TryGetValue(signature, out var sourceInfo))
                    continue;

                dto.RelatedKnowledgeId = sourceInfo.SourceKnowledgeId;
                dto.RelatedFrom = sourceInfo.RelatedFrom;
            }
        }

        result.Details = detailDtos;
        
        return new GetAiSpeechAssistantKnowledgeResponse { Data = result };
    }

    private static string EnsureCopySuffixForDetailMatching(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name ?? string.Empty;

        return name.EndsWith("-副本", StringComparison.Ordinal) ? name : $"{name}-副本";
    }

    private static string BuildDetailSignature(string name, AiSpeechAssistantKonwledgeFormatType formatType, string content, string fileName)
    {
        return $"{name ?? string.Empty}|{formatType}|{content ?? string.Empty}|{fileName ?? string.Empty}";
    }

    public async Task<List<AiSpeechAssistantKnowledgeCopyRelatedDto>> EnhanceRelateFrom(List<AiSpeechAssistantKnowledgeCopyRelated> relateds, CancellationToken cancellationToken)
    {
        if (relateds == null || relateds.Count == 0) return new List<AiSpeechAssistantKnowledgeCopyRelatedDto>();

        var sourceKnowledgeIds = relateds.Select(r => r.SourceKnowledgeId).Distinct().ToList();

        var sourceKnowledges = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgesAsync(sourceKnowledgeIds, cancellationToken).ConfigureAwait(false);

        var sourceKnowledgeMap = sourceKnowledges.ToDictionary(k => k.Id);

        var assistantIds = sourceKnowledges.Select(k => k.AssistantId).Distinct().ToList();

        var enrichInfos = await _aiSpeechAssistantDataProvider.GetKnowledgeCopyRelatedEnrichInfoAsync(assistantIds, cancellationToken).ConfigureAwait(false);

        var enrichDict = enrichInfos.ToDictionary(x => x.AssistantId);

        var result = new List<AiSpeechAssistantKnowledgeCopyRelatedDto>(relateds.Count);

        foreach (var related in relateds)
        {
            var dto = _mapper.Map<AiSpeechAssistantKnowledgeCopyRelatedDto>(related);

            if (sourceKnowledgeMap.TryGetValue(related.SourceKnowledgeId, out var sourceKnowledge) &&
                enrichDict.TryGetValue(sourceKnowledge.AssistantId, out var info))
            {
                dto.RelatedFrom = $"{info.StoreName} - {info.AiAgentName} - {info.AssiatantName}";
            }

            result.Add(dto);
        }

        return result;
    }

    public async Task<GetAiSpeechAssistantKnowledgeHistoryResponse> GetAiSpeechAssistantKnowledgeHistoryAsync(GetAiSpeechAssistantKnowledgeHistoryRequest request, CancellationToken cancellationToken)
    {
        var (count, knowledges) = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgesAsync(
            request.AssistantId, request.PageIndex, request.PageSize, cancellationToken: cancellationToken).ConfigureAwait(false);

        var knowledgeDtos = _mapper.Map<List<AiSpeechAssistantKnowledgeDto>>(knowledges);

        var historyKnowledges = await EnhanceKnowledgesHistoryRelatedInfo(knowledgeDtos, cancellationToken).ConfigureAwait(false);
        
        return new GetAiSpeechAssistantKnowledgeHistoryResponse
        {
            Data = new GetAiSpeechAssistantKnowledgeHistoryResponseData
            {
                Count = count,
                Knowledges = historyKnowledges
            }
        };
    }

    public async Task<List<AiSpeechAssistantKnowledgeDto>> EnhanceKnowledgesHistoryRelatedInfo(List<AiSpeechAssistantKnowledgeDto> knowledges, CancellationToken cancellationToken)
    {
        if (knowledges == null || knowledges.Count == 0) return knowledges;

        var ids = knowledges.Select(k => k.Id).Distinct().ToList();

        var allRelated = await _aiSpeechAssistantDataProvider.GetKnowledgeCopyRelatedByTargetKnowledgeIdAsync(ids, null, cancellationToken).ConfigureAwait(false);
        
        var relatedMap = allRelated.GroupBy(x => x.TargetKnowledgeId).ToDictionary(g => g.Key, g => g.ToList());

        knowledges.ForEach(k =>
        {
            relatedMap.TryGetValue(k.Id, out var relateds);

            k.KnowledgeCopyRelateds = _mapper.Map<List<AiSpeechAssistantKnowledgeCopyRelatedDto>>(relateds ?? new List<AiSpeechAssistantKnowledgeCopyRelated>());
        });
        
        return knowledges;
    }

    public async Task<GetAiSpeechAssistantByIdResponse> GetAiSpeechAssistantByIdAsync(GetAiSpeechAssistantByIdRequest request, CancellationToken cancellationToken)
    {
        var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantByIdAsync(request.AssistantId, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (assistant == null) throw new Exception("Could not found the assistant");
        
        var humanContact = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantHumanContactByAssistantIdAsync(assistant.Id, cancellationToken).ConfigureAwait(false);
        
        var enrichAssistant = _mapper.Map<AiSpeechAssistantDto>(assistant);
        enrichAssistant.TransferCallNumber = humanContact?.HumanPhone ?? string.Empty;
        
        return new GetAiSpeechAssistantByIdResponse
        {
            Data = enrichAssistant
        };
    }

    public async Task<GetAiSpeechAssistantSessionResponse> GetAiSpeechAssistantSessionAsync(GetAiSpeechAssistantSessionRequest request, CancellationToken cancellationToken)
    {
        var session = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantSessionBySessionIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);

        if (session == null) throw new Exception("Could not found the session");

        var sessionDto = _mapper.Map<AiSpeechAssistantSessionDto>(session);
        
        var premise = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantPremiseByAssistantIdAsync(session.AssistantId, cancellationToken).ConfigureAwait(false);

        if (premise != null)
            sessionDto.Premise = _mapper.Map<AiSpeechAssistantPremiseDto>(premise);
        
        return new GetAiSpeechAssistantSessionResponse
        {
            Data = sessionDto
        };
    }

    public async Task<GetAiSpeechAssistantInboundRoutesResponse> GetAiSpeechAssistantInboundRoutesAsync(GetAiSpeechAssistantInboundRoutesRequest request, CancellationToken cancellationToken)
    {
        var (count, routes) = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantInboundRoutesAsync(
            request.PageIndex, request.PageSize, request.AssistantId, request.Keyword, cancellationToken).ConfigureAwait(false);

        return new GetAiSpeechAssistantInboundRoutesResponse
        {
            Data = new GetAiSpeechAssistantInboundRoutesResponseData
            {
                Count = count,
                Routes = _mapper.Map<List<AiSpeechAssistantInboundRouteDto>>(routes)
            }
        };
    }

    private async Task EnrichAssistantsInfoAsync(List<AiSpeechAssistantDto> assistants, CancellationToken cancellationToken)
    {
        var assistantIds = assistants.Select(x => x.Id).ToList();

        var knowledges = _mapper.Map<List<AiSpeechAssistantKnowledgeDto>>(await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantActiveKnowledgesAsync(assistantIds, cancellationToken).ConfigureAwait(false));
        
        var humanContacts = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantHumanContactsAsync(assistantIds, cancellationToken).ConfigureAwait(false);
        
        foreach (var assistant in assistants)
        {
            assistant.Knowledge = knowledges.Where(k => k.AssistantId == assistant.Id).FirstOrDefault();
            
            assistant.TransferCallNumber = humanContacts.Where(h => h.AssistantId == assistant.Id).FirstOrDefault()?.HumanPhone ?? string.Empty;
        }
    }
}
