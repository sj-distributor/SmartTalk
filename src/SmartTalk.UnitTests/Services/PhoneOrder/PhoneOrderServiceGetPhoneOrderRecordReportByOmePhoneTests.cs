using AutoMapper;
using NSubstitute;
using Shouldly;
using SmartTalk.Core.Domain.PhoneOrder;
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
using SmartTalk.Core.Settings.PhoneOrder;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Requests.PhoneOrder;
using Xunit;

namespace SmartTalk.UnitTests.Services.PhoneOrder;

public class PhoneOrderServiceGetPhoneOrderRecordReportByOmePhoneTests
{
    private const int RecordId = 123;
    private const int FoundRecordId = 999;
    private const string CallerNumber = "0912345678";
    private static readonly DateTimeOffset CallTime = new(2026, 8, 24, 12, 30, 0, TimeSpan.Zero);

    private readonly IMapper _mapper = Substitute.For<IMapper>();
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider = Substitute.For<IPhoneOrderDataProvider>();

    [Fact]
    public async Task GetPhoneOrderRecordReportByOmePhoneAsync_WithRecordId_ShouldFetchRecordByIdAndReport()
    {
        var sut = CreateService();
        var record = new PhoneOrderRecord { Id = FoundRecordId };
        var report = new PhoneOrderRecordReport { RecordId = FoundRecordId };
        var expectedDto = new PhoneOrderRecordReportDto { RecordId = FoundRecordId };

        _phoneOrderDataProvider.GetPhoneOrderRecordByIdAsync(RecordId, Arg.Any<CancellationToken>()).Returns(record);
        _phoneOrderDataProvider.GetPhoneOrderRecordReportAsync(null, SystemLanguage.Chinese, Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(report);
        _mapper.Map<PhoneOrderRecordReportDto>(Arg.Any<object>()).Returns(expectedDto);

        var response = await sut.GetPhoneOrderRecordReportByOmePhoneAsync(new GetPhoneOrderRecordReportByOmePhoneRequest
        {
            RecordId = RecordId,
            Language = SystemLanguage.Chinese
        }, CancellationToken.None);

        await _phoneOrderDataProvider.Received(1).GetPhoneOrderRecordByIdAsync(RecordId, Arg.Any<CancellationToken>());
        await _phoneOrderDataProvider.DidNotReceive().GetPhoneOrderRecordByOmePhoneAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _phoneOrderDataProvider.Received(1).GetPhoneOrderRecordReportAsync(null, SystemLanguage.Chinese, FoundRecordId, Arg.Any<CancellationToken>());
        response.Data.ShouldBe(expectedDto);
    }

    [Fact]
    public async Task GetPhoneOrderRecordReportByOmePhoneAsync_WithoutRecordId_ShouldFetchRecordByCallerNumberAndCallTime()
    {
        var sut = CreateService();
        var record = new PhoneOrderRecord { Id = FoundRecordId };
        var report = new PhoneOrderRecordReport { RecordId = FoundRecordId };
        var expectedDto = new PhoneOrderRecordReportDto { RecordId = FoundRecordId };

        _phoneOrderDataProvider.GetPhoneOrderRecordByOmePhoneAsync(CallerNumber, CallTime, Arg.Any<CancellationToken>()).Returns(record);
        _phoneOrderDataProvider.GetPhoneOrderRecordReportAsync(null, SystemLanguage.Chinese, Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(report);
        _mapper.Map<PhoneOrderRecordReportDto>(Arg.Any<object>()).Returns(expectedDto);

        var response = await sut.GetPhoneOrderRecordReportByOmePhoneAsync(new GetPhoneOrderRecordReportByOmePhoneRequest
        {
            CallerNumber = CallerNumber,
            CallTime = CallTime,
            Language = SystemLanguage.Chinese
        }, CancellationToken.None);

        await _phoneOrderDataProvider.Received(1).GetPhoneOrderRecordByOmePhoneAsync(CallerNumber, CallTime, Arg.Any<CancellationToken>());
        await _phoneOrderDataProvider.DidNotReceive().GetPhoneOrderRecordByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _phoneOrderDataProvider.Received(1).GetPhoneOrderRecordReportAsync(null, SystemLanguage.Chinese, FoundRecordId, Arg.Any<CancellationToken>());
        response.Data.ShouldBe(expectedDto);
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
            Substitute.For<IPosDataProvider>(),
            null!,
            Substitute.For<IAgentDataProvider>(),
            Substitute.For<IAttachmentService>(),
            Substitute.For<ISpeechMaticsService>(),
            Substitute.For<ISpeechToTextService>(),
            Substitute.For<IPhoneOrderUtilService>(),
            Substitute.For<ISmartTalkHttpClientFactory>(),
            Substitute.For<IRestaurantDataProvider>(),
            _phoneOrderDataProvider,
            Substitute.For<ISmartTalkBackgroundJobClient>(),
            Substitute.For<ISpeechMaticsDataProvider>(),
            null!,
            Substitute.For<IAccountDataProvider>(),
            null!,
            null!,
            Substitute.For<ILinphoneDataProvider>());
    }
}
