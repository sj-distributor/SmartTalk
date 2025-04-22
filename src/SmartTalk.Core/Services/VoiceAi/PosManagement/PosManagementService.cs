using AutoMapper;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.VoiceAi.PosManagement;

public interface IPosManagementService : IScopedDependency
{
}

public class PosManagementService : IPosManagementService
{
    private readonly IMapper _mapper;
    private readonly IPosManagementDataProvider _posManagementDataProvider;

    public PosManagementService(IMapper mapper, IPosManagementDataProvider posManagementDataProvider)
    {
        _mapper = mapper;
        _posManagementDataProvider = posManagementDataProvider;
    }
}