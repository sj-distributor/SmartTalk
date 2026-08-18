using Mediator.Net;
using SmartTalk.Core.Domain.Pos;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Caching.Redis;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.KnowledgeScenario;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Core.Settings.Sales;

namespace SmartTalk.Core.Services.AiResourceSync;

public partial interface IAiResourceSyncService : IScopedDependency
{
    
}
public partial class AiResourceSyncService : IAiResourceSyncService
{
    private readonly ICrmClient _crmClient;
    private readonly IPosDataProvider _posDataProvider;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    private readonly ISalesDataProvider _salesDataProvider;
    private readonly SalesSetting _salesSetting;

    public AiResourceSyncService(
        ICrmClient crmClient,
        IPosDataProvider posDataProvider,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider,
        ISalesDataProvider salesDataProvider,
        SalesSetting salesSetting)
    {
        _crmClient = crmClient;
        _posDataProvider = posDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _salesDataProvider = salesDataProvider;
        _salesSetting = salesSetting;
    }
}