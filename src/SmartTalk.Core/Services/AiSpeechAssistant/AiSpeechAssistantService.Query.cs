using DocumentFormat.OpenXml.Office2010.ExcelAc;
using Serilog;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
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

        var allCopyRelateds = await _aiSpeechAssistantDataProvider.GetKnowledgeCopyRelatedByTargetKnowledgeIdAsync(
            new List<int> { knowledge.Id }, cancellationToken).ConfigureAwait(false);

        if (allCopyRelateds == null || !allCopyRelateds.Any())
        {
            allCopyRelateds = await _aiSpeechAssistantDataProvider.GetKnowledgeCopyRelatedBySourceKnowledgeIdAsync(new List<int> { knowledge.Id }, true, cancellationToken).ConfigureAwait(false);
        }

        if (allCopyRelateds == null || !allCopyRelateds.Any())
        {
            result.KnowledgeCopyRelateds = new List<AiSpeechAssistantKnowledgeCopyRelatedDto>();
        }
        else
        {
            result.KnowledgeCopyRelateds = _mapper.Map<List<AiSpeechAssistantKnowledgeCopyRelatedDto>>(allCopyRelateds);
        }
        
        return new GetAiSpeechAssistantKnowledgeResponse { Data = result };
    }
    
    public async Task<GetAiSpeechAssistantKnowledgeHistoryResponse> GetAiSpeechAssistantKnowledgeHistoryAsync(GetAiSpeechAssistantKnowledgeHistoryRequest request, CancellationToken cancellationToken)
    {
        var (count, knowledges) = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgesAsync(
            request.AssistantId, request.PageIndex, request.PageSize, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new GetAiSpeechAssistantKnowledgeHistoryResponse
        {
            Data = new GetAiSpeechAssistantKnowledgeHistoryResponseData
            {
                Count = count,
                Knowledges = _mapper.Map<List<AiSpeechAssistantKnowledgeDto>>(knowledges)
            }
        };
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

        return new GetAiSpeechAssistantSessionResponse
        {
            Data = _mapper.Map<AiSpeechAssistantSessionDto>(session)
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