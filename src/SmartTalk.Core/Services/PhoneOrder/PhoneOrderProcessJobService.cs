using Serilog;
using Smarties.Messages.Enums.System;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Messages.Commands.Restaurants;
using SmartTalk.Messages.Dto.WebSocket;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.PhoneOrder;

public interface IPhoneOrderProcessJobService : IScopedDependency
{
    Task ExtractFoodItemsFromConversationAsync(ExtractFoodItemsFromConversationCommand command, CancellationToken cancellationToken);
}

public class PhoneOrderProcessJobService : IPhoneOrderProcessJobService
{
    private readonly IPhoneOrderService _phoneOrderService;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly ISmartTalkBackgroundJobClient _smartTalkBackgroundJobClient;

    public PhoneOrderProcessJobService(IPhoneOrderService phoneOrderService, IPhoneOrderDataProvider phoneOrderDataProvider, ISmartTalkBackgroundJobClient smartTalkBackgroundJobClient)
    {
        _phoneOrderService = phoneOrderService;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _smartTalkBackgroundJobClient = smartTalkBackgroundJobClient;
    }

    public async Task ExtractFoodItemsFromConversationAsync(ExtractFoodItemsFromConversationCommand command, CancellationToken cancellationToken)
    {
        var conversations = await _phoneOrderDataProvider.GetPhoneOrderConversationsWithSpecificFieldAsync(cancellationToken).ConfigureAwait(false);

        Log.Information("Get the specific conversations : {@Conversations}", conversations);
        
        if (conversations == null || conversations.Count == 0) return;

        var record = await _phoneOrderDataProvider.GetPhoneOrderRecordByIdAsync(conversations.First().RecordId, cancellationToken).ConfigureAwait(false);

        Log.Information("Get the record by conversation: {@Record}", record);

        if (record == null) return;
        
        foreach (var conversation in conversations)
            _smartTalkBackgroundJobClient.Enqueue(() => EnhanceConversationAsync(conversation, record, cancellationToken), HangfireConstants.InternalHostingPhoneOrder);
    }

    public async Task EnhanceConversationAsync(PhoneOrderConversation conversation, PhoneOrderRecord record, CancellationToken cancellationToken)
    {
        var extractFoods = await _phoneOrderService.ExtractMenuItemsAsync(
            conversation.Intent!.Value, record, new PhoneOrderDetailDto(), conversation.Answer, cancellationToken).ConfigureAwait(false);
        
        var items = extractFoods.FoodDetails.Select(x => new PhoneOrderOrderItem
        {
            RecordId = record.Id,
            FoodName = x.FoodName,
            Quantity = x.Count ?? 0,
            Price = x.Price,
            Note = x.Remark
        }).ToList();

        if (items.Any())
            await _phoneOrderDataProvider.AddPhoneOrderItemAsync(items, true, cancellationToken).ConfigureAwait(false);
    }
}