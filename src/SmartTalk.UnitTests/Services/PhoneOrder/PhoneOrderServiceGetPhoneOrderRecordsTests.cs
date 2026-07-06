using AutoMapper;
using NSubstitute;
using SmartTalk.Core.Services.Account;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.Linphone;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Core.Services.STT;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Core.Settings.PhoneOrder;
using SmartTalk.Core.Settings.Sales;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;
using Xunit;

namespace SmartTalk.UnitTests.Services.PhoneOrder;

public class PhoneOrderServiceGetPhoneOrderRecordsTests
{
    private readonly IMapper _mapper = Substitute.For<IMapper>();
    private readonly IPosDataProvider _posDataProvider = Substitute.For<IPosDataProvider>();
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider = Substitute.For<IPhoneOrderDataProvider>();

    [Fact]
    public async Task GetPhoneOrderRecordsAsync_ShouldPassOrderIdsAndAssistantIdToDataProvider()
    {
        var sut = CreateService();
        var request = new GetPhoneOrderRecordsRequest
        {
            AgentId = 2800,
            AssistantId = 1265,
            OrderIds = ["09b85b3d-98c4-4c73-b940-500f9bdf1a0e", "00000000-0000-0000-0000-000000000001"]
        };

        _phoneOrderDataProvider
            .GetPhoneOrderRecordsAsync(
                Arg.Any<List<int>>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<List<DialogueScenarios>>(),
                Arg.Any<int?>(),
                Arg.Any<List<string>>(),
                Arg.Any<CancellationToken>())
            .Returns([]);
        _mapper.Map<List<PhoneOrderRecordDto>>(Arg.Any<object>()).Returns([]);
        _posDataProvider.GetAiDraftOrderRecordIdsByRecordIdsAsync(Arg.Any<List<int>>(), Arg.Any<CancellationToken>()).Returns([]);
        _phoneOrderDataProvider.GetPhoneOrderReservationInfoUnreviewedRecordIdsAsync(Arg.Any<List<int>>(), Arg.Any<CancellationToken>()).Returns([]);

        await sut.GetPhoneOrderRecordsAsync(request, CancellationToken.None);

        await _phoneOrderDataProvider.Received(1).GetPhoneOrderRecordsAsync(
            Arg.Is<List<int>>(x => x.SequenceEqual(new[] { 2800 })),
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<List<DialogueScenarios>>(),
            1265,
            Arg.Is<List<string>>(x => x.SequenceEqual(request.OrderIds)),
            Arg.Any<CancellationToken>());
    }

    private PhoneOrderService CreateService()
    {
        return new PhoneOrderService(
            _mapper,
            Substitute.For<ICurrentUser>(),
            null!,
            Substitute.For<IWeChatClient>(),
            Substitute.For<IEasyPosClient>(),
            Substitute.For<IFfmpegService>(),
            Substitute.For<ISmartiesClient>(),
            _posDataProvider,
            null!,
            Substitute.For<IAgentDataProvider>(),
            Substitute.For<IAttachmentService>(),
            Substitute.For<IAccountDataProvider>(),
            Substitute.For<ISpeechMaticsService>(),
            Substitute.For<ISpeechToTextService>(),
            Substitute.For<IPhoneOrderUtilService>(),
            Substitute.For<ISmartTalkHttpClientFactory>(),
            Substitute.For<IRestaurantDataProvider>(),
            _phoneOrderDataProvider,
            Substitute.For<ISmartTalkBackgroundJobClient>(),
            Substitute.For<ISpeechMaticsDataProvider>(),
            null!,
            null!,
            Substitute.For<IAiSpeechAssistantDataProvider>(),
            Substitute.For<ILinphoneDataProvider>());
    }
}
