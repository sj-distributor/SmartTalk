using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.PhoneOrder;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderDataProvider
{
    Task<List<PhoneOrderConversationOpenAi>> AddPhoneOrderOpenAiConversationsAsync(List<PhoneOrderConversationOpenAi> conversations, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeletePhoneOrderOpenAiConversationsAsync(int recordId, CancellationToken cancellationToken);
}

public partial class PhoneOrderDataProvider
{
    public async Task<List<PhoneOrderConversationOpenAi>> AddPhoneOrderOpenAiConversationsAsync(List<PhoneOrderConversationOpenAi> conversations, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(conversations, cancellationToken).ConfigureAwait(false);

        if (forceSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return conversations;
    }

    public async Task DeletePhoneOrderOpenAiConversationsAsync(int recordId, CancellationToken cancellationToken)
    {
        var conversations = await _repository.Query<PhoneOrderConversationOpenAi>(x => x.RecordId == recordId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (conversations.Any())
            await _repository.DeleteAllAsync(conversations, cancellationToken).ConfigureAwait(false);
    }
}
