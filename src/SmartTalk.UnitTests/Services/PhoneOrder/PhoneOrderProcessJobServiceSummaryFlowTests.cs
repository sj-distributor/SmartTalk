using Google.Cloud.Translation.V2;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Domain.SpeechMatics;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Caching;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Core.Services.Twilio;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Core.Settings.Twilio;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.STT;
using Xunit;

namespace SmartTalk.UnitTests.Services.PhoneOrder;

public class PhoneOrderProcessJobServiceSummaryFlowTests
{
    [Fact]
    public async Task HandleReleasedDiarizedTranscribeAsync_ShortAiGreetingOnlyCall_ShouldCompleteWithFixedSummary()
    {
        var fixture = new FlowFixture("00:00:05", SingleSpeakerGreetingTranscription);

        await fixture.Service.HandleReleasedDiarizedTranscribeAsync(
            fixture.Record.Id,
            CancellationToken.None);

        fixture.Service.OriginalSummaryCallCount.ShouldBe(0);
        fixture.Record.Duration.ShouldBe(5);
        fixture.Record.Status.ShouldBe(PhoneOrderRecordStatus.Sent);
        fixture.Record.Scenario.ShouldBe(DialogueScenarios.InvalidCall);
        fixture.Record.IsCompleted.ShouldBeTrue();
        fixture.SavedReports.ShouldNotBeNull();
        fixture.SavedReports.Count.ShouldBe(2);
        fixture.SavedReports.Single(x => x.Language == TranscriptionLanguage.English)
            .Report.ShouldBe(fixture.Record.TranscriptionText);

        await fixture.PhoneOrderService.Received(1).ProcessPhoneOrderDiarizedTranscriptionAsync(
            Arg.Is<List<PhoneOrderDiarizedSpeakInfoDto>>(x =>
                x.Count == 1 &&
                x.Select(segment => segment.Speaker).Distinct().Count() == 1),
            fixture.Record,
            false,
            Arg.Any<CancellationToken>());
        await fixture.PhoneOrderDataProvider.Received(1)
            .MarkRecordCompletedAsync(fixture.Record.Id, Arg.Any<CancellationToken>());
        await fixture.SmartiesClient.Received(1).CallBackSmartiesAiSpeechAssistantRecordAsync(
            Arg.Is<AiSpeechAssistantCallBackRequestDto>(x =>
                x.CallSid == fixture.Record.SessionId &&
                x.RecordUrl == fixture.Record.Url &&
                x.RecordAnalyzeReport == fixture.Record.TranscriptionText),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleReleasedDiarizedTranscribeAsync_AboveThresholdSingleSpeakerCall_ShouldCompleteWithFixedSummary()
    {
        var fixture = new FlowFixture("00:00:07.92", SingleSpeakerGreetingTranscription);

        await fixture.Service.HandleReleasedDiarizedTranscribeAsync(
            fixture.Record.Id,
            CancellationToken.None);

        fixture.Service.OriginalSummaryCallCount.ShouldBe(0);
        fixture.Record.Duration.ShouldBe(7.92);
        fixture.Record.Status.ShouldBe(PhoneOrderRecordStatus.Sent);
        fixture.Record.Scenario.ShouldBe(DialogueScenarios.InvalidCall);
        fixture.Record.IsCompleted.ShouldBeTrue();

        await fixture.PhoneOrderService.Received(1).ProcessPhoneOrderDiarizedTranscriptionAsync(
            Arg.Any<List<PhoneOrderDiarizedSpeakInfoDto>>(),
            fixture.Record,
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleReleasedSpeechMaticsCallBackAsync_AboveThresholdSingleSpeakerCall_ShouldCompleteWithFixedSummary()
    {
        const string jobId = "speechmatics-job-42";
        var fixture = new FlowFixture("00:00:07.92", SingleSpeakerGreetingTranscription);

        fixture.SpeechMaticsDataProvider
            .GetSpeechMaticsJobAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(new SpeechMaticsJob
            {
                JobId = jobId,
                CallbackMessage = SingleSpeakerSpeechMaticsCallback
            });
        fixture.PhoneOrderDataProvider
            .GetPhoneOrderRecordByTranscriptionJobIdAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(fixture.Record);

        await fixture.Service.HandleReleasedSpeechMaticsCallBackAsync(jobId, CancellationToken.None);

        fixture.Service.OriginalSummaryCallCount.ShouldBe(0);
        fixture.Record.Duration.ShouldBe(7.92);
        fixture.Record.Status.ShouldBe(PhoneOrderRecordStatus.Sent);
        fixture.Record.Scenario.ShouldBe(DialogueScenarios.InvalidCall);
        fixture.Record.IsCompleted.ShouldBeTrue();

        await fixture.PhoneOrderService.Received(1).ExtractPhoneOrderRecordAiMenuAsync(
            Arg.Any<List<SmartTalk.Messages.Dto.SpeechMatics.SpeechMaticsSpeakInfoDto>>(),
            fixture.Record,
            Arg.Any<byte[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleReleasedDiarizedTranscribeAsync_LongTwoSpeakerCall_ShouldUseOriginalSummaryAndOptimization()
    {
        var fixture = new FlowFixture("00:01:30", TwoSpeakerTranscription);

        await fixture.Service.HandleReleasedDiarizedTranscribeAsync(
            fixture.Record.Id,
            CancellationToken.None);

        fixture.Service.OriginalSummaryCallCount.ShouldBe(1);
        fixture.Record.Duration.ShouldBe(90);
        fixture.Record.Status.ShouldBe(PhoneOrderRecordStatus.Sent);
        fixture.Record.TranscriptionText.ShouldBe(TestPhoneOrderProcessJobService.OriginalSummary);

        await fixture.PhoneOrderService.Received(1).ProcessPhoneOrderDiarizedTranscriptionAsync(
            Arg.Any<List<PhoneOrderDiarizedSpeakInfoDto>>(),
            fixture.Record,
            true,
            Arg.Any<CancellationToken>());
        await fixture.PhoneOrderDataProvider.DidNotReceiveWithAnyArgs()
            .MarkRecordCompletedAsync(default);
        await fixture.SmartiesClient.DidNotReceiveWithAnyArgs()
            .CallBackSmartiesAiSpeechAssistantRecordAsync(default!, default);
    }

    [Fact]
    public async Task HandleReleasedDiarizedTranscribeAsync_UnparseableDuration_ShouldTreatDurationAsUnknown()
    {
        var fixture = new FlowFixture(string.Empty, TwoSpeakerTranscription);

        await fixture.Service.HandleReleasedDiarizedTranscribeAsync(
            fixture.Record.Id,
            CancellationToken.None);

        fixture.Record.Duration.ShouldBeNull();
        fixture.Service.OriginalSummaryCallCount.ShouldBe(1);

        await fixture.PhoneOrderService.Received(1).ProcessPhoneOrderDiarizedTranscriptionAsync(
            Arg.Any<List<PhoneOrderDiarizedSpeakInfoDto>>(),
            fixture.Record,
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleReleasedDiarizedTranscribeAsync_NoDiarizedSegments_ShouldFallBackToOriginalSummary()
    {
        var fixture = new FlowFixture("00:01:30", "{ \"segments\": [] }");

        await fixture.Service.HandleReleasedDiarizedTranscribeAsync(
            fixture.Record.Id,
            CancellationToken.None);

        fixture.Service.OriginalSummaryCallCount.ShouldBe(1);
        fixture.Record.Status.ShouldBe(PhoneOrderRecordStatus.Sent);

        await fixture.PhoneOrderService.Received(1).ProcessPhoneOrderDiarizedTranscriptionAsync(
            Arg.Is<List<PhoneOrderDiarizedSpeakInfoDto>>(x => x.Count == 0),
            fixture.Record,
            true,
            Arg.Any<CancellationToken>());
        await fixture.PhoneOrderDataProvider.DidNotReceiveWithAnyArgs()
            .MarkRecordCompletedAsync(default);
    }

    private const string TwoSpeakerTranscription = """
        {
          "segments": [
            { "start": 0.0, "end": 1.2, "speaker": "speaker_0", "text": "Hello, how can I help?" },
            { "start": 1.3, "end": 3.0, "speaker": "speaker_1", "text": "I would like two cases of water." }
          ]
        }
        """;

    private const string SingleSpeakerGreetingTranscription = """
        {
          "segments": [
            { "start": 0.0, "end": 3.0, "speaker": "speaker_0", "text": "Hello, are you there?" },
            { "start": 3.1, "end": 5.0, "speaker": "speaker_0", "text": "Please call us back." }
          ]
        }
        """;

    private const string SingleSpeakerSpeechMaticsCallback = """
        {
          "results": [
            {
              "start_time": 0.4,
              "end_time": 0.9,
              "alternatives": [
                { "speaker": "S1", "content": "Hello," }
              ]
            },
            {
              "start_time": 1.05,
              "end_time": 2.9,
              "alternatives": [
                { "speaker": "S1", "content": "this is OME. How can I help you today?" }
              ]
            }
          ]
        }
        """;

    private sealed class FlowFixture
    {
        private readonly byte[] _audioContent = [1, 2, 3, 4];

        public FlowFixture(string duration, string diarizedTranscription)
        {
            Record = new PhoneOrderRecord
            {
                Id = 42,
                AgentId = 7,
                AssistantId = 8,
                SessionId = "call-session-42",
                Url = "https://recordings.test/call-42.wav",
                PhoneNumber = "+19255550100",
                Language = TranscriptionLanguage.English
            };

            var agent = new Agent
            {
                Id = Record.AgentId,
                Type = AgentType.Sales
            };
            var assistant = new AiSpeechAssistant
            {
                Id = Record.AssistantId.Value,
                AgentId = Record.AgentId,
                Name = "Test assistant"
            };

            PhoneOrderDataProvider
                .GetPhoneOrderRecordAsync(Record.Id, cancellationToken: Arg.Any<CancellationToken>())
                .Returns([Record]);
            SmartTalkHttpClientFactory
                .GetAsync<byte[]>(Record.Url, Arg.Any<CancellationToken>())
                .Returns(_audioContent);
            FfmpegService
                .GetAudioDurationAsync(_audioContent, Arg.Any<CancellationToken>())
                .Returns(duration);
            OpenaiClient
                .TranscribeDiarizedAudioAsync(_audioContent, "recording.wav", Arg.Any<CancellationToken>())
                .Returns(diarizedTranscription);
            AiSpeechAssistantDataProvider
                .GetAgentAndAiSpeechAssistantAsync(
                    Record.AgentId,
                    Record.AssistantId,
                    Arg.Any<CancellationToken>())
                .Returns((assistant, agent));

            PhoneOrderDataProvider
                .When(x => x.AddPhoneOrderRecordReportsAsync(
                    Arg.Any<List<PhoneOrderRecordReport>>(),
                    true,
                    Arg.Any<CancellationToken>()))
                .Do(callInfo => SavedReports = callInfo.Arg<List<PhoneOrderRecordReport>>());

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OpenAi:ApiKey"] = "test-api-key"
                })
                .Build();

            Service = new TestPhoneOrderProcessJobService(
                SalesClient,
                FfmpegService,
                TwilioService,
                new TwilioSettings(configuration),
                new OpenAiSettings(configuration),
                OpenaiClient,
                SmartiesClient,
                TranslationClient,
                PhoneOrderService,
                SalesDataProvider,
                PhoneOrderDataProvider,
                SmartTalkHttpClient,
                SpeechMaticsDataProvider,
                SmartTalkHttpClientFactory,
                BackgroundJobClient,
                SmartTalkBackgroundJobClient,
                AiSpeechAssistantDataProvider,
                PosUtilService,
                PhoneOrderUtilService,
                SalesCustomerMatchService,
                PosDataProvider,
                CacheManager);
        }

        public PhoneOrderRecord Record { get; }

        public TestPhoneOrderProcessJobService Service { get; }

        public List<PhoneOrderRecordReport>? SavedReports { get; private set; }

        public ISalesClient SalesClient { get; } = Substitute.For<ISalesClient>();

        public IFfmpegService FfmpegService { get; } = Substitute.For<IFfmpegService>();

        public ITwilioService TwilioService { get; } = Substitute.For<ITwilioService>();

        public IOpenaiClient OpenaiClient { get; } = Substitute.For<IOpenaiClient>();

        public ISmartiesClient SmartiesClient { get; } = Substitute.For<ISmartiesClient>();

        public TranslationClient TranslationClient { get; } = Substitute.For<TranslationClient>();

        public IPhoneOrderService PhoneOrderService { get; } = Substitute.For<IPhoneOrderService>();

        public ISalesDataProvider SalesDataProvider { get; } = Substitute.For<ISalesDataProvider>();

        public IPhoneOrderDataProvider PhoneOrderDataProvider { get; } = Substitute.For<IPhoneOrderDataProvider>();

        public ISmartTalkHttpClientFactory SmartTalkHttpClient { get; } = Substitute.For<ISmartTalkHttpClientFactory>();

        public ISpeechMaticsDataProvider SpeechMaticsDataProvider { get; } = Substitute.For<ISpeechMaticsDataProvider>();

        public ISmartTalkHttpClientFactory SmartTalkHttpClientFactory { get; } = Substitute.For<ISmartTalkHttpClientFactory>();

        public ISmartTalkBackgroundJobClient BackgroundJobClient { get; } = Substitute.For<ISmartTalkBackgroundJobClient>();

        public ISmartTalkBackgroundJobClient SmartTalkBackgroundJobClient { get; } = Substitute.For<ISmartTalkBackgroundJobClient>();

        public IAiSpeechAssistantDataProvider AiSpeechAssistantDataProvider { get; } = Substitute.For<IAiSpeechAssistantDataProvider>();

        public IPosUtilService PosUtilService { get; } = Substitute.For<IPosUtilService>();

        public IPhoneOrderUtilService PhoneOrderUtilService { get; } = Substitute.For<IPhoneOrderUtilService>();

        public ISalesCustomerMatchService SalesCustomerMatchService { get; } = Substitute.For<ISalesCustomerMatchService>();

        public IPosDataProvider PosDataProvider { get; } = Substitute.For<IPosDataProvider>();

        public ICacheManager CacheManager { get; } = Substitute.For<ICacheManager>();
    }

    private sealed class TestPhoneOrderProcessJobService : PhoneOrderProcessJobService
    {
        internal const string OriginalSummary = "Original summary path";

        public TestPhoneOrderProcessJobService(
            ISalesClient salesClient,
            IFfmpegService ffmpegService,
            ITwilioService twilioService,
            TwilioSettings twilioSettings,
            OpenAiSettings openAiSettings,
            IOpenaiClient openaiClient,
            ISmartiesClient smartiesClient,
            TranslationClient translationClient,
            IPhoneOrderService phoneOrderService,
            ISalesDataProvider salesDataProvider,
            IPhoneOrderDataProvider phoneOrderDataProvider,
            ISmartTalkHttpClientFactory smartTalkHttpClient,
            ISpeechMaticsDataProvider speechMaticsDataProvider,
            ISmartTalkHttpClientFactory smartTalkHttpClientFactory,
            ISmartTalkBackgroundJobClient backgroundJobClient,
            ISmartTalkBackgroundJobClient smartTalkBackgroundJobClient,
            IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider,
            IPosUtilService posUtilService,
            IPhoneOrderUtilService phoneOrderUtilService,
            ISalesCustomerMatchService salesCustomerMatchService,
            IPosDataProvider posDataProvider,
            ICacheManager cacheManager)
            : base(
                salesClient,
                ffmpegService,
                twilioService,
                twilioSettings,
                openAiSettings,
                openaiClient,
                smartiesClient,
                translationClient,
                phoneOrderService,
                salesDataProvider,
                phoneOrderDataProvider,
                smartTalkHttpClient,
                speechMaticsDataProvider,
                smartTalkHttpClientFactory,
                backgroundJobClient,
                smartTalkBackgroundJobClient,
                aiSpeechAssistantDataProvider,
                posUtilService,
                phoneOrderUtilService,
                salesCustomerMatchService,
                posDataProvider,
                cacheManager)
        {
        }

        public int OriginalSummaryCallCount { get; private set; }

        internal override Task SummarizeConversationContentAsync(
            PhoneOrderRecord record,
            byte[] audioContent,
            CancellationToken cancellationToken)
        {
            OriginalSummaryCallCount++;
            record.Status = PhoneOrderRecordStatus.Sent;
            record.TranscriptionText = OriginalSummary;
            return Task.CompletedTask;
        }
    }
}
