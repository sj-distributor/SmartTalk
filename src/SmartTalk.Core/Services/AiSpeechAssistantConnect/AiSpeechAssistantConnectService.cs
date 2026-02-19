using Serilog;
using AutoMapper;
using Newtonsoft.Json;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Infrastructure;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.RealtimeAiV2.Services;
using SmartTalk.Core.Services.AiSpeechAssistantConnect.Exceptions;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Events.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public interface IAiSpeechAssistantConnectService : IScopedDependency
{
    Task<AiSpeechAssistantConnectCloseEvent> ConnectAsync(ConnectAiSpeechAssistantCommand command, CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantConnectService : IAiSpeechAssistantConnectService
{
    private AiSpeechAssistantConnectContext _ctx;

    private readonly IClock _clock;
    private readonly IMapper _mapper;

    #region Data Providers

    private readonly IPosDataProvider _posDataProvider;
    private readonly ISalesDataProvider _salesDataProvider;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    #endregion

    #region Services

    private readonly IFfmpegService _ffmpegService;
    private readonly IRealtimeAiService _realtimeAiService;

    #endregion

    #region Clients

    private readonly IOpenaiClient _openaiClient;
    private readonly ISmartiesClient _smartiesClient;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;

    #endregion
    
    public AiSpeechAssistantConnectService(
        IClock clock, 
        IMapper mapper, 
        IPosDataProvider posDataProvider, 
        ISalesDataProvider salesDataProvider,
        IAgentDataProvider agentDataProvider, 
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider, 
        IFfmpegService ffmpegService, 
        IRealtimeAiService realtimeAiService, 
        IOpenaiClient openaiClient, 
        ISmartiesClient smartiesClient, 
        ISmartTalkBackgroundJobClient backgroundJobClient)
    {
        _clock = clock;
        _mapper = mapper;
        _posDataProvider = posDataProvider;
        _salesDataProvider = salesDataProvider;
        _agentDataProvider = agentDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _ffmpegService = ffmpegService;
        _realtimeAiService = realtimeAiService;
        _openaiClient = openaiClient;
        _smartiesClient = smartiesClient;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<AiSpeechAssistantConnectCloseEvent> ConnectAsync(ConnectAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        Log.Information("[AiAssistant] Call connected, From: {From}, To: {To}", command.From, command.To);

        try
        {
            _ctx = BuildContext(command);

            var agent = await ResolveActiveAgentAsync(cancellationToken).ConfigureAwait(false);

            await ForwardIfRequiredAsync(cancellationToken).ConfigureAwait(false);

            EnsureServiceAvailable(agent);

            var options = await BuildSessionConfigAsync(cancellationToken).ConfigureAwait(false);

            Log.Information("[AiAssistant] Starting AI session, AssistantId: {AssistantId}, Provider: {Provider}, From: {From}, To: {To}", _ctx.Assistant.Id, _ctx.Assistant.ModelProvider, _ctx.From, _ctx.To);

            await _realtimeAiService.ConnectAsync(options, cancellationToken).ConfigureAwait(false);
        }
        catch (AiAssistantNotAvailableException ex)
        {
            Log.Information("[AiAssistant] {Reason}, From: {From}, To: {To}", ex.Message, command.From, command.To);
        }
        catch (AiAssistantCallForwardedException ex)
        {
            Log.Information("[AiAssistant] {Reason}, From: {From}, To: {To}", ex.Message, command.From, command.To);
        }

        return new AiSpeechAssistantConnectCloseEvent();
    }

    private async Task<Agent> ResolveActiveAgentAsync(CancellationToken cancellationToken)
    {
        var agent = await _agentDataProvider.GetAgentByNumberAsync(_ctx.To, _ctx.AssistantId, cancellationToken).ConfigureAwait(false);

        if (agent?.IsReceiveCall != true)
            throw new AiAssistantNotAvailableException("No active agent");

        _ctx.AgentId = agent.Id;
        _ctx.TransferCallNumber = agent.TransferCallNumber;

        return agent;
    }

    private async Task ForwardIfRequiredAsync(CancellationToken cancellationToken)
    {
        var forwardNumber = await ResolveInboundRouteAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(forwardNumber)) return;

        Log.Information("[AiAssistant] Forwarding call, ForwardNumber: {ForwardNumber}, From: {From}, To: {To}", forwardNumber, _ctx.From, _ctx.To);

        await HandleForwardOnlyAsync(forwardNumber, cancellationToken).ConfigureAwait(false);

        throw new AiAssistantCallForwardedException("Call forwarded");
    }

    private void EnsureServiceAvailable(Agent agent)
    {
        (_ctx.IsInAiServiceHours, _ctx.IsEnableManualService) = CheckIfInServiceHours(
            agent.ServiceHours, agent.IsTransferHuman, agent.TransferCallNumber, _clock.Now);

        Log.Information("[AiAssistant] Service hours checked, InService: {InService}, ManualFallback: {ManualFallback}", _ctx.IsInAiServiceHours, _ctx.IsEnableManualService);

        if (!_ctx.IsInAiServiceHours && !_ctx.IsEnableManualService)
            throw new AiAssistantNotAvailableException("Out of service hours, no manual fallback");
    }

    public static (bool IsInServiceHours, bool IsEnableManualService) CheckIfInServiceHours(
        string serviceHoursJson, bool isTransferHuman, string transferCallNumber, DateTimeOffset utcNow)
    {
        if (serviceHoursJson == null)
            return (true, isTransferHuman && !string.IsNullOrEmpty(transferCallNumber));

        var pstZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var pstTime = TimeZoneInfo.ConvertTime(utcNow, pstZone);

        var workingHours = JsonConvert.DeserializeObject<List<AgentServiceHoursDto>>(serviceHoursJson);
        var specificWorkingHours = workingHours?.FirstOrDefault(x => x.DayOfWeek == pstTime.DayOfWeek);

        var pstTimeToMinute = new TimeSpan(pstTime.TimeOfDay.Hours, pstTime.TimeOfDay.Minutes, 0);

        var isInService = specificWorkingHours != null &&
                          specificWorkingHours.Hours.Any(x => x.Start <= pstTimeToMinute && x.End >= pstTimeToMinute);

        return (isInService, isTransferHuman && !string.IsNullOrEmpty(transferCallNumber));
    }
}
