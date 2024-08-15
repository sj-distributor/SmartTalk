using SmartTalk.Core.Domain.System;
using SmartTalk.Messages.Dto.System;
using SmartTalk.Messages.Enums.Account;
using SmartTalk.Messages.Enums.System;

namespace SmartTalk.Core.Services.System;

public partial class VerificationCodeService
{
    public async Task<VerifyCodeResult> VerifyAsync(VerifyCodeParams @params, CancellationToken cancellationToken)
    {
        var result = new VerifyCodeResult();
        
        var verificationCode = await _verificationCodeDataProvider.GetVerificationCodeAsync(
            @params.UserAccountId, @params.Identity, @params.Recipient, verificationMethod: @params.VerificationMethod, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (verificationCode == null)
        {
            result.FailedReason = VerifyCodeFailedReason.NotFound;
            return result;
        }
        if (IsCodeExpired(verificationCode))
        {
            result.FailedReason = VerifyCodeFailedReason.CodeExpired;
        }
        else if (ExceedsMaxAttempts(verificationCode))
        {
            result.FailedReason = VerifyCodeFailedReason.MaxAttemptsExceeded;
        }
        else if (!IsCodeCorrect(verificationCode, @params.Code))
        {
            result.FailedReason = VerifyCodeFailedReason.CodeIncorrect;
        }

        await ProcessVerificationResultAsync(result, verificationCode, cancellationToken).ConfigureAwait(false);
        
        result.VerificationCode = _mapper.Map<VerificationCodeDto>(verificationCode);

        return result;
    }
    
    private async Task ProcessVerificationResultAsync(VerifyCodeResult result, VerificationCode code, CancellationToken cancellationToken)
    {
        if (result.IsValid)
        {
            code.AuthenticatedDate = _clock.Now;
            code.AuthenticationStatus = UserAccountVerificationCodeAuthenticationStatus.Authenticated;
        }
        else
        {
            code.FailedAttempts += 1;
        }
        
        await _verificationCodeDataProvider.UpdateAsync(code, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}