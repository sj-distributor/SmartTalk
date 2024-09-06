using AutoMapper;
using Google.Cloud.Translation.V2;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.STT;
using SmartTalk.Core.Settings.PhoneOrder;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderService : IScopedDependency
{
}

public partial class PhoneOrderService : IPhoneOrderService
{
    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IWeChatClient _weChatClient;
    private readonly IFfmpegService _ffmpegService;
    private readonly ISmartiesClient _smartiesClient;
    private readonly TranslationClient _translationClient;
    private readonly PhoneOrderSetting _phoneOrderSetting;
    private readonly IAttachmentService _attachmentService;
    private readonly SpeechMaticsClient _speechMaticsClient;
    private readonly ISpeechToTextService _speechToTextService;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly TranscriptionCallbackSetting _transcriptionCallbackSetting;

    public PhoneOrderService(
        IMapper mapper,
        ICurrentUser currentUser,
        IWeChatClient weChatClient,
        IFfmpegService ffmpegService,
        ISmartiesClient smartiesClient,
        TranslationClient translationClient,
        PhoneOrderSetting phoneOrderSetting,
        IAttachmentService attachmentService,
        ISpeechToTextService speechToTextService,
        SpeechMaticsClient speechMaticsClient,
        IPhoneOrderDataProvider phoneOrderDataProvider,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        TranscriptionCallbackSetting transcriptionCallbackSetting)
    {
        _mapper = mapper;
        _currentUser = currentUser;
        _weChatClient = weChatClient;
        _ffmpegService = ffmpegService;
        _smartiesClient = smartiesClient;
        _translationClient = translationClient;
        _phoneOrderSetting = phoneOrderSetting;
        _attachmentService = attachmentService;
        _speechToTextService = speechToTextService;
        _speechMaticsClient = speechMaticsClient;
        _backgroundJobClient = backgroundJobClient;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _transcriptionCallbackSetting = transcriptionCallbackSetting;
    }
}