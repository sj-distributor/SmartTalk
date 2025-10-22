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
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Restaurants;
using SmartTalk.Core.Services.RetrievalDb.VectorDb;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Core.Services.STT;
using SmartTalk.Core.Settings.PhoneOrder;
using SmartTalk.Core.Settings.SpeechMatics;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderService : IScopedDependency
{
}

public partial class PhoneOrderService : IPhoneOrderService
{
    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IWeChatClient _weChatClient;
    private readonly IEasyPosClient _easyPosClient;
    private readonly IFfmpegService _ffmpegService;
    private readonly ISmartiesClient _smartiesClient;
    private readonly TranslationClient _translationClient;
    private readonly IPosDataProvider _posDataProvider;
    private readonly IAttachmentService _attachmentService;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly ISpeechMaticsService _speechMaticsService;
    private readonly ISpeechToTextService _speechToTextService;
    private readonly IPhoneOrderUtilService _phoneOrderUtilService;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly IRestaurantDataProvider _restaurantDataProvider;

    public PhoneOrderService(
        IMapper mapper,
        ICurrentUser currentUser,
        IWeChatClient weChatClient,
        IEasyPosClient easyPosClient,
        IFfmpegService ffmpegService,
        ISmartiesClient smartiesClient,
        IPosDataProvider posDataProvider,
        TranslationClient translationClient,
        IAgentDataProvider agentDataProvider,
        IAttachmentService attachmentService,
        ISpeechMaticsService speechMaticsService,
        ISpeechToTextService speechToTextService,
        IPhoneOrderUtilService phoneOrderUtilService,
        ISmartTalkHttpClientFactory httpClientFactory,
        IRestaurantDataProvider restaurantDataProvider,
        IPhoneOrderDataProvider phoneOrderDataProvider)
    {
        _mapper = mapper;
        _currentUser = currentUser;
        _weChatClient = weChatClient;
        _easyPosClient = easyPosClient;
        _ffmpegService = ffmpegService;
        _smartiesClient = smartiesClient;
        _translationClient = translationClient;
        _posDataProvider = posDataProvider;
        _attachmentService = attachmentService;
        _agentDataProvider = agentDataProvider;
        _httpClientFactory = httpClientFactory;
        _speechToTextService = speechToTextService;
        _speechMaticsService = speechMaticsService;
        _phoneOrderUtilService = phoneOrderUtilService;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _restaurantDataProvider = restaurantDataProvider;
    }
}