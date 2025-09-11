using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Account;
using SmartTalk.Messages.Enums;
using SmartTalk.Messages.Enums.STT;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Security;

public class SwitchLanguageCommand : ICommand
{
    public SystemLanguage Language { get; set; }
}

public class SwitchLanguageResponse : SmartTalkResponse<UserAccountDto>
{
}