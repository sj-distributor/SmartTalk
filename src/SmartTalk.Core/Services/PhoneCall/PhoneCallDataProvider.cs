using AutoMapper;
using SmartTalk.Core.Data;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.PhoneCall;

public partial interface IPhoneCallDataProvider : IScopedDependency
{
}

public partial class PhoneCallDataProvider : IPhoneCallDataProvider
{
    private readonly IMapper _mapper;
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public PhoneCallDataProvider(IMapper mapper, IRepository repository, IUnitOfWork unitOfWork)
    {
        _mapper = mapper;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }
}