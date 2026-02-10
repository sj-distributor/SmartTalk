using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Messages.Commands.RealtimeAi;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Dto.Smarties;

namespace SmartTalk.Core.Services.RealtimeAi.Services;

public interface IAiSpeechAssistantRealtimeService : IScopedDependency
{
    Task RealtimeAiConnectAsync(RealtimeAiConnectCommand command, CancellationToken cancellationToken);
}

public class AiSpeechAssistantRealtimeService : IAiSpeechAssistantRealtimeService
{
    private readonly IRealtimeAiService _realtimeAiService;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    public AiSpeechAssistantRealtimeService(
        IRealtimeAiService realtimeAiService,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _realtimeAiService = realtimeAiService;
        _backgroundJobClient = backgroundJobClient;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
    }

    public async Task RealtimeAiConnectAsync(RealtimeAiConnectCommand command, CancellationToken cancellationToken)
    {
        var assistant = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantWithKnowledgeAsync(command.AssistantId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not find assistant by id: {command.AssistantId}");

        var timer = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantTimerByAssistantIdAsync(assistant.Id, cancellationToken).ConfigureAwait(false);
        assistant.Timer = timer;

        var options = new RealtimeSessionOptions
        {
            WebSocket = command.WebSocket,
            AssistantProfile = assistant,
            InitialPrompt = "You are a friendly assistant",
            InputFormat = command.InputFormat,
            OutputFormat = command.OutputFormat,
            Region = command.Region
        };

        var orderRecordType = command.OrderRecordType;

        var callbacks = new RealtimeSessionCallbacks
        {
            OnRecordingSavedAsync = (fileUrl, sessionId) =>
            {
                if (!string.IsNullOrEmpty(fileUrl) && assistant.Id != 0)
                {
                    _backgroundJobClient.Enqueue<IRealtimeProcessJobService>(x =>
                        x.RecordingRealtimeAiAsync(fileUrl, assistant.Id, sessionId, orderRecordType, CancellationToken.None));
                }

                return Task.CompletedTask;
            },
            OnTranscriptionsReadyAsync = async (sessionId, transcriptions) =>
            {
                var kid = await _aiSpeechAssistantDataProvider.GetAiKidAsync(agentId: assistant.AgentId).ConfigureAwait(false);
                if (kid == null) return;

                _backgroundJobClient.Enqueue<ISmartiesClient>(x =>
                    x.CallBackSmartiesAiKidConversationsAsync(new AiKidConversationCallBackRequestDto
                    {
                        Uuid = kid.KidUuid,
                        SessionId = sessionId,
                        Transcriptions = transcriptions.Select(t => new RealtimeAiTranscriptionDto
                        {
                            Speaker = t.Speaker,
                            Transcription = t.Text
                        }).ToList()
                    }, CancellationToken.None));
            },
            IdleFollowUp = timer != null
                ? new RealtimeSessionIdleFollowUp
                {
                    TimeoutSeconds = timer.TimeSpanSeconds,
                    FollowUpMessage = timer.AlterContent,
                    SkipRounds = timer.SkipRound
                }
                : null
        };

        await _realtimeAiService.StartAsync(options, callbacks, cancellationToken).ConfigureAwait(false);
    }
}