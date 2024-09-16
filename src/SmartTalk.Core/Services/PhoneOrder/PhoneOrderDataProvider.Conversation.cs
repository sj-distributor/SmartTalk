using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderDataProvider
{
    Task<List<PhoneOrderConversation>> GetPhoneOrderConversationsAsync(int recordId, CancellationToken cancellationToken);

    Task<List<PhoneOrderConversation>> AddPhoneOrderConversationsAsync(List<PhoneOrderConversation> conversations, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeletePhoneOrderConversationsAsync(int recordId, CancellationToken cancellationToken);

    Task<List<PhoneOrderConversation>> GetPhoneOrderConversationsWithSpecificFieldAsync(CancellationToken cancellationToken);
}

public partial class PhoneOrderDataProvider
{
    public async Task<List<PhoneOrderConversation>> GetPhoneOrderConversationsAsync(int recordId, CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneOrderConversation>(x => x.RecordId == recordId).OrderBy(x => x.Order).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PhoneOrderConversation>> AddPhoneOrderConversationsAsync(List<PhoneOrderConversation> conversations, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(conversations, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return conversations;
    }

    public async Task DeletePhoneOrderConversationsAsync(int recordId, CancellationToken cancellationToken)
    {
        var conversations = await _repository.Query<PhoneOrderConversation>(x => x.RecordId == recordId).ToListAsync(cancellationToken).ConfigureAwait(false);

        if (conversations.Any()) await _repository.DeleteAllAsync(conversations, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PhoneOrderConversation>> GetPhoneOrderConversationsWithSpecificFieldAsync(CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneOrderConversation>()
            .Where(x => (x.Intent == PhoneOrderIntent.AddOrder || x.Intent == PhoneOrderIntent.AskDishes)
                        && (string.IsNullOrEmpty(x.ExtractFoodItem) || x.ExtractFoodItem == "[]"))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}