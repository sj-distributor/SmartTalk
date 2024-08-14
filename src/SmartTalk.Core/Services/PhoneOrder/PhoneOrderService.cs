using AutoMapper;
using SmartTalk.Core.Ioc;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderService : IScopedDependency
{
}

public partial class PhoneOrderService : IPhoneOrderService
{
    private readonly IMapper _mapper;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;

    public PhoneOrderService(IMapper mapper, IPhoneOrderDataProvider phoneOrderDataProvider)
    {
        _mapper = mapper;
        _phoneOrderDataProvider = phoneOrderDataProvider;
    }
}