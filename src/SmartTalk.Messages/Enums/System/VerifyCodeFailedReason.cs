namespace SmartTalk.Messages.Enums.System;

public enum VerifyCodeFailedReason
{
    None,
    NotFound,
    CodeExpired,
    CodeIncorrect,
    MaxAttemptsExceeded
}