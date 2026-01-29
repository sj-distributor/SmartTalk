using AutoMapper;
using Google.Cloud.Translation.V2;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Account;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.Linphone;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Core.Services.RetrievalDb.VectorDb;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Core.Services.STT;
using SmartTalk.Core.Settings.PhoneOrder;
using SmartTalk.Core.Settings.Sales;
using SmartTalk.Core.Settings.SpeechMatics;
using SmartTalk.Core.Services.AiSpeechAssistant;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderService : IScopedDependency
{
}

public partial class PhoneOrderService : IPhoneOrderService
{
    private readonly IMapper _mapper;
    private readonly IVectorDb _vectorDb;
    private readonly ICurrentUser _currentUser;
    private readonly IWeChatClient _weChatClient;
    private readonly IEasyPosClient _easyPosClient;
    private readonly IFfmpegService _ffmpegService;
    private readonly ISmartiesClient _smartiesClient;
    private readonly TranslationClient _translationClient;
    private readonly PhoneOrderSetting _phoneOrderSetting;
    private readonly IPosDataProvider _posDataProvider;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    private readonly IAccountDataProvider _accountDataProvider;
    private readonly ILinphoneDataProvider _linphoneDataProvider;
    private readonly IAttachmentService _attachmentService;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly SpeechMaticsClient _speechMaticsClient;
    private readonly ISpeechToTextService _speechToTextService;
    private readonly IPhoneOrderUtilService _phoneOrderUtilService;
    private readonly SpeechMaticsKeySetting _speechMaticsKeySetting;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly IRestaurantDataProvider _restaurantDataProvider;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly ISpeechMaticsDataProvider _speechMaticsDataProvider;
    private readonly TranscriptionCallbackSetting _transcriptionCallbackSetting;
    private readonly SalesSetting _salesSetting;

    public PhoneOrderService(
        IMapper mapper,
        IVectorDb vectorDb,
        ICurrentUser currentUser,
        SalesSetting salesSetting,
        IWeChatClient weChatClient,
        IEasyPosClient easyPosClient,
        IFfmpegService ffmpegService,
        ISmartiesClient smartiesClient,
        TranslationClient translationClient,
        PhoneOrderSetting phoneOrderSetting,
        IPosDataProvider posDataProvider,
        IAccountDataProvider accountDataProvider,
        IAttachmentService attachmentService,
        IAgentDataProvider agentDataProvider,
        ISpeechToTextService speechToTextService,
        SpeechMaticsClient speechMaticsClient,
        IPhoneOrderUtilService phoneOrderUtilService,
        SpeechMaticsKeySetting speechMaticsKeySetting,
        ISmartTalkHttpClientFactory httpClientFactory,
        IRestaurantDataProvider restaurantDataProvider,
        IPhoneOrderDataProvider phoneOrderDataProvider,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        ISpeechMaticsDataProvider speechMaticsDataProvider,
        TranscriptionCallbackSetting transcriptionCallbackSetting,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider,
        ILinphoneDataProvider linphoneDataProvider)
    {
        _mapper = mapper;
        _vectorDb = vectorDb;
        _currentUser = currentUser;
        _weChatClient = weChatClient;
        _easyPosClient = easyPosClient;
        _ffmpegService = ffmpegService;
        _smartiesClient = smartiesClient;
        _translationClient = translationClient;
        _phoneOrderSetting = phoneOrderSetting;
        _posDataProvider = posDataProvider;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _accountDataProvider = accountDataProvider;
        _attachmentService = attachmentService;
        _agentDataProvider = agentDataProvider;
        _httpClientFactory = httpClientFactory;
        _speechToTextService = speechToTextService;
        _speechMaticsClient = speechMaticsClient;
        _backgroundJobClient = backgroundJobClient;
        _phoneOrderUtilService = phoneOrderUtilService;
        _speechMaticsKeySetting = speechMaticsKeySetting;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _restaurantDataProvider = restaurantDataProvider;
        _speechMaticsDataProvider = speechMaticsDataProvider;
        _transcriptionCallbackSetting = transcriptionCallbackSetting;
        _linphoneDataProvider = linphoneDataProvider;
        _salesSetting = salesSetting;
    }
}
