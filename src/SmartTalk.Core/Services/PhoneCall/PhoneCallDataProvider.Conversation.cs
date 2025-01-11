using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Domain.PhoneCall;

namespace SmartTalk.Core.Services.PhoneCall;

public partial interface IPhoneCallDataProvider
{
    Task<List<PhoneCallConversation>> GetPhoneOrderConversationsAsync(int recordId, CancellationToken cancellationToken);

    Task<List<PhoneCallConversation>> AddPhoneOrderConversationsAsync(List<PhoneCallConversation> conversations, bool forceSave = true, CancellationToken cancellationToken = default);

    Task DeletePhoneOrderConversationsAsync(int recordId, CancellationToken cancellationToken);
}

public partial class PhoneCallDataProvider
{
    public async Task<List<PhoneCallConversation>> GetPhoneOrderConversationsAsync(int recordId, CancellationToken cancellationToken)
    {
        return await _repository.Query<PhoneCallConversation>(x => x.RecordId == recordId).OrderBy(x => x.Order).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PhoneCallConversation>> AddPhoneOrderConversationsAsync(List<PhoneCallConversation> conversations, bool forceSave = true, CancellationToken cancellationToken = default)
    {
        await _repository.InsertAllAsync(conversations, cancellationToken).ConfigureAwait(false);

        if (forceSave) await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return conversations;
    }

    public async Task DeletePhoneOrderConversationsAsync(int recordId, CancellationToken cancellationToken)
    {
        var conversations = await _repository.Query<PhoneCallConversation>(x => x.RecordId == recordId).ToListAsync(cancellationToken).ConfigureAwait(false);

        if (conversations.Any()) await _repository.DeleteAllAsync(conversations, cancellationToken).ConfigureAwait(false);
    }
}