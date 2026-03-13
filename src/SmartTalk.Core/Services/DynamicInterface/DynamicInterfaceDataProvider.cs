using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.Sales;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.DynamicInterface;

public interface IDynamicInterfaceDataProvider : IScopedDependency
{
    Task<List<AiSpeechAssistantKnowledgeVariableCache>> GetDynamicInterfaceNodesAsync(CancellationToken cancellationToken);
}

public class DynamicInterfaceDataProvider : IDynamicInterfaceDataProvider
{
    private readonly IMapper _mapper;
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DynamicInterfaceDataProvider(IMapper mapper, IRepository repository, IUnitOfWork unitOfWork)
    {
        _mapper = mapper;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<AiSpeechAssistantKnowledgeVariableCache>> GetDynamicInterfaceNodesAsync(CancellationToken cancellationToken)
    {
        return await _repository.Query<AiSpeechAssistantKnowledgeVariableCache>().OrderBy(x => x.LevelType).ThenBy(x => x.Id).ToListAsync(cancellationToken);
    }
}