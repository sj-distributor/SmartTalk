using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Account;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Security;

public class UpdateUserAccountTaskNotificationCommand : ICommand
{
    public int? UserId { get; set; }
    
    public int? StoreId {get; set;}
    
    public bool? IsTaskEnabled { get; set; }

    public bool? IsTurnOnNotification { get; set; }
}

public class UpdateUserAccountTaskNotificationResponse: SmartTalkResponse<UpdateUserAccountTaskNotificationResponseData>
{
}

public class UpdateUserAccountTaskNotificationResponseData
{
    public UserAccountDto UserAccount {get; set;}
    
    public CompanyStoreDto CompanyStore {get; set;}
}
