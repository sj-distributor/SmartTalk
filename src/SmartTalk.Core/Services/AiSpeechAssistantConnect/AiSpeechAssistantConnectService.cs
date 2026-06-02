using System.Net.WebSockets;
using Serilog;
using AutoMapper;
using Newtonsoft.Json;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Utils;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Core.Services.SjFood;
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
    private readonly ISjFoodQuotationService _sjFoodQuotationService;

    #endregion

    #region Services

    private readonly IFfmpegService _ffmpegService;
    private readonly IPosUtilService _posUtilService;
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
        ISjFoodQuotationService sjFoodQuotationService,
        IFfmpegService ffmpegService, 
        IPosUtilService posUtilService,
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
        _sjFoodQuotationService = sjFoodQuotationService;
        _ffmpegService = ffmpegService;
        _posUtilService = posUtilService;
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

            EnsureServiceAvailable(agent);

            await ForwardIfRequiredAsync(cancellationToken).ConfigureAwait(false);

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
        catch (Exception ex)
        {
            Log.Error(ex, "[AiAssistant] Unhandled error during connect, From: {From}, To: {To}", command.From, command.To);
        }
        finally
        {
            await TryCloseTwilioWebSocketAsync(command.TwilioWebSocket).ConfigureAwait(false);
        }

        return new AiSpeechAssistantConnectCloseEvent();
    }

    /// <summary>
    /// Best-effort close of the Twilio WebSocket. Used by ConnectAsync's finally block
    /// to guarantee the socket is released even when an unknown exception escapes — without
    /// this, the socket dangles until the platform's idle timeout (~30s) and the caller
    /// hears silence. No-op when the socket is already closed/aborted/null. Swallows all
    /// errors during close because we cannot do better than best-effort here.
    /// Public static for unit testability; not intended for external use.
    /// </summary>
    public static async Task TryCloseTwilioWebSocketAsync(WebSocket twilioWebSocket)
    {
        if (twilioWebSocket is null) return;
        if (twilioWebSocket.State is WebSocketState.Closed or WebSocketState.Aborted or WebSocketState.None) return;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        try
        {
            await twilioWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session ended", cts.Token).ConfigureAwait(false);
        }
        catch (WebSocketException) { }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) { }
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

        var targetNumber = !_ctx.IsInAiServiceHours && _ctx.IsEnableManualService ? _ctx.TransferCallNumber : forwardNumber;

        Log.Information("[AiAssistant] Forwarding call, ForwardNumber: {ForwardNumber}, TargetNumber: {TargetNumber}, From: {From}, To: {To}", forwardNumber, targetNumber, _ctx.From, _ctx.To);

        await HandleForwardOnlyAsync(targetNumber, cancellationToken).ConfigureAwait(false);

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

        var pstTime = TimeZoneInfo.ConvertTime(utcNow, PstTimeZone.Get());

        var workingHours = JsonConvert.DeserializeObject<List<AgentServiceHoursDto>>(serviceHoursJson);
        var specificWorkingHours = workingHours?.FirstOrDefault(x => x.DayOfWeek == pstTime.DayOfWeek);

        var pstTimeToMinute = new TimeSpan(pstTime.TimeOfDay.Hours, pstTime.TimeOfDay.Minutes, 0);

        var isInService = specificWorkingHours != null &&
                          specificWorkingHours.Hours.Any(x => x.Start <= pstTimeToMinute && x.End >= pstTimeToMinute);

        return (isInService, isTransferHuman && !string.IsNullOrEmpty(transferCallNumber));
    }
}
