using SmartTalk.Core.Domain.AISpeechAssistant;
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
        var (count, assistants) = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantsAsync(
            request.PageIndex, request.PageSize, cancellationToken).ConfigureAwait(false);

        await EnrichAssistantsInfoAsycn(assistants, cancellationToken).ConfigureAwait(false);

        return new GetAiSpeechAssistantsResponse
        {
            Data = new GetAiSpeechAssistantsResponseData
            {
                Count = count,
                Assistants = _mapper.Map<List<AiSpeechAssistantDto>>(assistants)
            }
        };
    }

    public async Task<GetAiSpeechAssistantKnowledgeResponse> GetAiSpeechAssistantKnowledgeAsync(GetAiSpeechAssistantKnowledgeRequest request, CancellationToken cancellationToken)
    {
        var knowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(
            request.AssistantId, request.KnowledgeId, request.KnowledgeId.HasValue ? null : true, cancellationToken).ConfigureAwait(false);

        return new GetAiSpeechAssistantKnowledgeResponse
        {
            Data = _mapper.Map<AiSpeechAssistantKnowledgeDto>(knowledge)
        };
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
        
        return new GetAiSpeechAssistantByIdResponse
        {
            Data = _mapper.Map<AiSpeechAssistantDto>(assistant)
        };
    }

    private async Task EnrichAssistantsInfoAsycn(List<Domain.AISpeechAssistant.AiSpeechAssistant> assistants, CancellationToken cancellationToken)
    {
        var knowledges = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantActiveKnowledgesAsync(
                assistants.Select(x => x.Id).ToList(), cancellationToken).ConfigureAwait(false);
        
        assistants.ForEach(x => x.Knowledge = knowledges.Where(k => k.AssistantId == x.Id).FirstOrDefault());
    }
}