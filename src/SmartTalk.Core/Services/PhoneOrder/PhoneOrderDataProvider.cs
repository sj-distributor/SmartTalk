using AutoMapper;
using SmartTalk.Core.Data;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderDataProvider : IScopedDependency
{
}

public partial class PhoneOrderDataProvider : IPhoneOrderDataProvider
{
    private readonly IMapper _mapper;
    private readonly IRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public PhoneOrderDataProvider(IMapper mapper, IRepository repository, IUnitOfWork unitOfWork)
    {
        _mapper = mapper;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }
}