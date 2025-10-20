using Serilog;
using Newtonsoft.Json;
using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Services.AutoTest;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Messages.Enums.SpeechMatics;
using SmartTalk.Messages.Commands.SpeechMatics;

namespace SmartTalk.Core.Handlers.CommandHandlers.SpeechMatics;

public class DistributeSpeechMaticsCallbackCommandHandler : ICommandHandler<DistributeSpeechMaticsCallbackCommand>
{
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly ISpeechMaticsDataProvider _speechMaticsDataProvider;

    public DistributeSpeechMaticsCallbackCommandHandler(ISmartTalkBackgroundJobClient backgroundJobClient, ISpeechMaticsDataProvider speechMaticsDataProvider)
    {
        _backgroundJobClient = backgroundJobClient;
        _speechMaticsDataProvider = speechMaticsDataProvider;
    }

    public async Task Handle(IReceiveContext<DistributeSpeechMaticsCallbackCommand> context, CancellationToken cancellationToken)
    {
        var callBack = JsonConvert.DeserializeObject<SpeechMaticsCallBackResponseDto>(context.Message.CallBackMessage);
        
        var job = await _speechMaticsDataProvider.GetSpeechMaticsJobAsync(callBack.Job.Id, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Retrieving speech matics job: {@Job}, jobId: {JobId}", job, callBack.Job.Id);

        if (job == null) return;

        job.CallbackMessage = context.Message.CallBackMessage;
        await _speechMaticsDataProvider.UpdateSpeechMaticsJobAsync(job, true, cancellationToken).ConfigureAwait(false);

        switch (job.Scenario)
        {
            case SpeechMaticsJobScenario.Released:
                _backgroundJobClient.Enqueue<IPhoneOrderProcessJobService>(x => x.HandleReleasedSpeechMaticsCallBackAsync(job.JobId, job.ScenarioRecordId, cancellationToken), HangfireConstants.InternalHostingPhoneOrder);
                break;
            
            case SpeechMaticsJobScenario.Testing:
                _backgroundJobClient.Enqueue<IAutoTestProcessJobService>(x => x.HandleTestingSpeechMaticsCallBackAsync(job.JobId, cancellationToken));
                break;
        }
    }
}