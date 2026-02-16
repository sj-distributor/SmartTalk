using AutoMapper;
using Mediator.Net;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Infrastructure;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Services;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.Messages.Events.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public interface IAiSpeechAssistantConnectService : IScopedDependency
{
    Task<AiSpeechAssistantConnectCloseEvent> ConnectAsync(
        ConnectAiSpeechAssistantCommand command, CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantConnectService : IAiSpeechAssistantConnectService
{
    private readonly IClock _clock;
    private readonly IMapper _mapper;
    private readonly IRealtimeAiService _realtimeAiService;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    private readonly IPosDataProvider _posDataProvider;
    private readonly ISalesDataProvider _salesDataProvider;
    private readonly ISmartiesClient _smartiesClient;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly IOpenaiClient _openaiClient;
    private readonly IFfmpegService _ffmpegService;

    public AiSpeechAssistantConnectService(
        IClock clock,
        IMapper mapper,
        IRealtimeAiService realtimeAiService,
        IAgentDataProvider agentDataProvider,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider,
        IPosDataProvider posDataProvider,
        ISalesDataProvider salesDataProvider,
        ISmartiesClient smartiesClient,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        IOpenaiClient openaiClient,
        IFfmpegService ffmpegService)
    {
        _clock = clock;
        _mapper = mapper;
        _realtimeAiService = realtimeAiService;
        _agentDataProvider = agentDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _posDataProvider = posDataProvider;
        _salesDataProvider = salesDataProvider;
        _smartiesClient = smartiesClient;
        _backgroundJobClient = backgroundJobClient;
        _openaiClient = openaiClient;
        _ffmpegService = ffmpegService;
    }

    public async Task<AiSpeechAssistantConnectCloseEvent> ConnectAsync(
        ConnectAiSpeechAssistantCommand command, CancellationToken cancellationToken)
    {
        Log.Information("The call from {From} to {To} is connected", command.From, command.To);

        var agent = await _agentDataProvider
            .GetAgentByNumberAsync(command.To, command.AssistantId, cancellationToken).ConfigureAwait(false);

        Log.Information("Get the agent: {@Agent} by {AssistantId} or {DidNumber}", agent, command.AssistantId, command.To);

        if (agent == null || agent.IsReceiveCall == false) return new AiSpeechAssistantConnectCloseEvent();

        var ctx = new SessionBusinessContext
        {
            Host = command.Host,
            CallerNumber = command.From,
            TransferCallNumber = agent.TransferCallNumber,
            LastUserInfo = new AiSpeechAssistantUserInfoDto { PhoneNumber = command.From }
        };

        var resolvedPrompt = await BuildKnowledgeBaseAsync(
            ctx, command.From, command.To, command.AssistantId, command.NumberId, agent.Id, cancellationToken).ConfigureAwait(false);

        CheckIfInServiceHours(agent, ctx);

        if (!ctx.IsInAiServiceHours && !ctx.IsEnableManualService)
            return new AiSpeechAssistantConnectCloseEvent();

        if (!ctx.ShouldForward)
        {
            ctx.HumanContactPhone = (await _aiSpeechAssistantDataProvider
                .GetAiSpeechAssistantHumanContactByAssistantIdAsync(ctx.Assistant.Id, cancellationToken)
                .ConfigureAwait(false))?.HumanPhone;
        }

        if (ctx.ShouldForward)
        {
            await HandleForwardOnlyAsync(command.TwilioWebSocket, ctx, command.OrderRecordType, cancellationToken).ConfigureAwait(false);
            return new AiSpeechAssistantConnectCloseEvent();
        }

        var modelConfig = await BuildModelConfigAsync(ctx, resolvedPrompt, cancellationToken).ConfigureAwait(false);

        var timer = await _aiSpeechAssistantDataProvider
            .GetAiSpeechAssistantTimerByAssistantIdAsync(ctx.Assistant.Id, cancellationToken).ConfigureAwait(false);

        var options = BuildSessionOptions(command, ctx, modelConfig, timer);

        await _realtimeAiService.ConnectAsync(options, cancellationToken).ConfigureAwait(false);

        return new AiSpeechAssistantConnectCloseEvent();
    }

    private static void CheckIfInServiceHours(Agent agent, SessionBusinessContext ctx)
    {
        if (agent.ServiceHours == null)
        {
            ctx.IsInAiServiceHours = true;
            return;
        }

        var utcNow = DateTimeOffset.UtcNow;
        var pstZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var pstTime = TimeZoneInfo.ConvertTime(utcNow, pstZone);
        var dayOfWeek = pstTime.DayOfWeek;

        var workingHours = JsonConvert.DeserializeObject<List<AgentServiceHoursDto>>(agent.ServiceHours);

        Log.Information("Parsed service hours; {@WorkingHours}", workingHours);

        var specificWorkingHours = workingHours.FirstOrDefault(x => x.DayOfWeek == dayOfWeek);

        Log.Information("Matched specific service hours: {@SpecificWorkingHours} and the pstTime: {@PstTime}", specificWorkingHours, pstTime);

        var pstTimeToMinute = new TimeSpan(pstTime.TimeOfDay.Hours, pstTime.TimeOfDay.Minutes, 0);

        ctx.IsInAiServiceHours = specificWorkingHours != null &&
                                  specificWorkingHours.Hours.Any(x => x.Start <= pstTimeToMinute && x.End >= pstTimeToMinute);
        ctx.IsEnableManualService = agent.IsTransferHuman && !string.IsNullOrEmpty(agent.TransferCallNumber);
    }

    private class SessionBusinessContext
    {
        public string CallSid { get; set; }
        public string StreamSid { get; set; }
        public string Host { get; set; }
        public string CallerNumber { get; set; }

        public AiSpeechAssistantDto Assistant { get; set; }
        public AiSpeechAssistantKnowledgeDto Knowledge { get; set; }

        public bool ShouldForward { get; set; }
        public string ForwardPhoneNumber { get; set; }
        public string HumanContactPhone { get; set; }
        public string TransferCallNumber { get; set; }
        public bool IsInAiServiceHours { get; set; } = true;
        public bool IsEnableManualService { get; set; }

        public string ModelName { get; set; }

        public bool IsTransfer { get; set; }
        public AiSpeechAssistantOrderDto OrderItems { get; set; }
        public AiSpeechAssistantUserInfoDto UserInfo { get; set; }
        public AiSpeechAssistantUserInfoDto LastUserInfo { get; set; }
    }
}
