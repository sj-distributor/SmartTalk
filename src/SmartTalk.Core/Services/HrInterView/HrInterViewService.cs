using AutoMapper;
using SmartTalk.Core.Domain.HrInterView;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.HrInterView;
using SmartTalk.Messages.Dto.HrInterView;
using SmartTalk.Messages.Requests.HrInterView;

namespace SmartTalk.Core.Services.HrInterView;

public interface IHrInterViewService : IScopedDependency
{
    Task<AddOrUpdateHrInterViewSettingResponse> AddOrUpdateHrInterViewSettingAsync(AddOrUpdateHrInterViewSettingCommand command, CancellationToken cancellationToken);
    
    Task<GetHrInterViewSettingsResponse> GetHrInterViewSettingsAsync(GetHrInterViewSettingsRequest request, CancellationToken cancellationToken);
}

public class HrInterViewService : IHrInterViewService
{
    private readonly IMapper _mapper;
    private readonly IHrInterViewDataProvider _hrInterViewDataProvider;

    public HrInterViewService(IMapper mapper, IHrInterViewDataProvider hrInterViewDataProvider)
    {
        _mapper = mapper;
        _hrInterViewDataProvider = hrInterViewDataProvider;
    }

    public async Task<AddOrUpdateHrInterViewSettingResponse> AddOrUpdateHrInterViewSettingAsync(AddOrUpdateHrInterViewSettingCommand command, CancellationToken cancellationToken)
    {
        var setting = _mapper.Map<HrInterViewSetting>(command.Setting);
        
        var exists = await _hrInterViewDataProvider.GetHrInterViewSettingByIdAsync(command.Setting.Id, cancellationToken).ConfigureAwait(false);

        if (exists == null) await _hrInterViewDataProvider.AddHrInterViewSettingAsync(setting, cancellationToken:cancellationToken).ConfigureAwait(false);
        else await _hrInterViewDataProvider.UpdateHrInterViewSettingAsync(setting, cancellationToken:cancellationToken).ConfigureAwait(false);

        var existsQuestion = await _hrInterViewDataProvider.GetHrInterViewSettingQuestionsAsync(command.Questions.Select(x => x.Id).ToList(), cancellationToken).ConfigureAwait(false);
        
        if (existsQuestion.Any()) await _hrInterViewDataProvider.DeleteHrInterViewSettingQuestionsAsync(existsQuestion, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        await _hrInterViewDataProvider.UpdateHrInterViewSettingQuestionsAsync(_mapper.Map<List<HrInterViewSettingQuestion>>(command.Questions), cancellationToken: cancellationToken).ConfigureAwait(false);
        
        //TODO
        
        throw new NotImplementedException();
    }

    public async Task<GetHrInterViewSettingsResponse> GetHrInterViewSettingsAsync(GetHrInterViewSettingsRequest request, CancellationToken cancellationToken)
    {
        var settings = await _hrInterViewDataProvider.GetHrInterViewSettingsAsync(cancellationToken).ConfigureAwait(false);
        
        return new GetHrInterViewSettingsResponse
        {
            Data = _mapper.Map<List<HrInterViewSettingDto>>(settings)
        };
    }
}