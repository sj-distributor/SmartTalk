using AutoMapper;
using Google.Cloud.Translation.V2;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Core.Services.RetrievalDb.VectorDb;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Core.Services.STT;
using SmartTalk.Core.Settings.PhoneCall;
using SmartTalk.Core.Settings.SpeechMatics;

namespace SmartTalk.Core.Services.PhoneCall;

public partial interface IPhoneCallService : IScopedDependency
{
}

public partial class PhoneCallService : IPhoneCallService
{
    private readonly IMapper _mapper;
    private readonly IVectorDb _vectorDb;
    private readonly ICurrentUser _currentUser;
    private readonly IWeChatClient _weChatClient;
    private readonly IEasyPosClient _easyPosClient;
    private readonly IFfmpegService _ffmpegService;
    private readonly ISmartiesClient _smartiesClient;
    private readonly TranslationClient _translationClient;
    private readonly PhoneCallSetting _phoneCallSetting;
    private readonly IAttachmentService _attachmentService;
    private readonly SpeechMaticsClient _speechMaticsClient;
    private readonly ISpeechToTextService _speechToTextService;
    private readonly IPhoneCallUtilService _phoneCallUtilService;
    private readonly SpeechMaticsKeySetting _speechMaticsKeySetting;
    private readonly IPhoneCallDataProvider _phoneCallDataProvider;
    private readonly IRestaurantDataProvider _restaurantDataProvider;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly ISpeechMaticsDataProvider _speechMaticsDataProvider;
    private readonly TranscriptionCallbackSetting _transcriptionCallbackSetting;

    public PhoneCallService(
        IMapper mapper,
        IVectorDb vectorDb,
        ICurrentUser currentUser,
        IWeChatClient weChatClient,
        IEasyPosClient easyPosClient,
        IFfmpegService ffmpegService,
        ISmartiesClient smartiesClient,
        TranslationClient translationClient,
        PhoneCallSetting phoneCallSetting,
        IAttachmentService attachmentService,
        ISpeechToTextService speechToTextService,
        SpeechMaticsClient speechMaticsClient,
        IPhoneCallUtilService phoneCalUtilService,
        SpeechMaticsKeySetting speechMaticsKeySetting,
        IRestaurantDataProvider restaurantDataProvider,
        IPhoneCallDataProvider phoneCallDataProvider,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        ISpeechMaticsDataProvider speechMaticsDataProvider,
        TranscriptionCallbackSetting transcriptionCallbackSetting)
    {
        _mapper = mapper;
        _vectorDb = vectorDb;
        _currentUser = currentUser;
        _weChatClient = weChatClient;
        _easyPosClient = easyPosClient;
        _ffmpegService = ffmpegService;
        _smartiesClient = smartiesClient;
        _translationClient = translationClient;
        _phoneCallSetting = phoneCallSetting;
        _attachmentService = attachmentService;
        _speechToTextService = speechToTextService;
        _speechMaticsClient = speechMaticsClient;
        _backgroundJobClient = backgroundJobClient;
        _phoneCallUtilService = phoneCalUtilService;
        _speechMaticsKeySetting = speechMaticsKeySetting;
        _phoneCallDataProvider = phoneCallDataProvider;
        _restaurantDataProvider = restaurantDataProvider;
        _speechMaticsDataProvider = speechMaticsDataProvider;
        _transcriptionCallbackSetting = transcriptionCallbackSetting;
    }
}