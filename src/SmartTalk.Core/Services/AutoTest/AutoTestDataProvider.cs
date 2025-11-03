using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestDataProvider : IScopedDependency
{
}

public partial class AutoTestDataProvider : IAutoTestDataProvider
{
    private readonly IMapper _mapper;
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    
    public AutoTestDataProvider(IRepository repository, IMapper mapper, IUnitOfWork unitOfWork, IAgentDataProvider agentDataProvider, IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _mapper = mapper;
        _unitOfWork = unitOfWork;
        _repository = repository;
        _agentDataProvider = agentDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
    }
    

 

   
    



}