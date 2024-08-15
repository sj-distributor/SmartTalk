using AutoMapper;
using SmartTalk.Core.Domain.System;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Infrastructure;
using SmartTalk.Messages.Dto.System;

namespace SmartTalk.Core.Services.System;

public interface IVerificationCodeService : IScopedDependency
{
    Task<VerifyCodeResult> VerifyAsync(VerifyCodeParams @params, CancellationToken cancellationToken);
}

public partial class VerificationCodeService : IVerificationCodeService
{
    private readonly IClock _clock;
    private readonly IMapper _mapper;
    private readonly IVerificationCodeDataProvider _verificationCodeDataProvider;

    public VerificationCodeService(
        IClock clock,
        IMapper mapper,
        IVerificationCodeDataProvider verificationCodeDataProvider)
    {
        _clock = clock;
        _mapper = mapper;
        _verificationCodeDataProvider = verificationCodeDataProvider;
    }
    
    private bool IsCodeExpired(VerificationCode code)
    {
        return code.ExpiredDate < _clock.Now;
    }
    
    private bool IsCodeCorrect(VerificationCode code, string inputCode)
    {
        return string.Equals(code.Code, inputCode, StringComparison.OrdinalIgnoreCase);
    }

    private bool ExceedsMaxAttempts(VerificationCode code, int maxAttempts = 5)
    {
        return code.FailedAttempts >= maxAttempts;
    }
}