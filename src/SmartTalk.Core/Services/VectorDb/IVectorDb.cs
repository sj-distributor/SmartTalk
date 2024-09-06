using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.VectorDb;

namespace SmartTalk.Core.Services.RetrievalDb.VectorDb;

public interface IVectorDb : IScopedDependency
{
    Task CreateIndexAsync(string index, int vectorSize, CancellationToken cancellationToken = default);

    Task<IEnumerable<string>> GetIndexesAsync(CancellationToken cancellationToken = default);

    Task DeleteIndexAsync(string index, CancellationToken cancellationToken = default);

    Task<string> UpsertAsync(string index, VectorRecordDto record, CancellationToken cancellationToken = default);

    Task DeleteAsync(string index, VectorRecordDto record, CancellationToken cancellationToken = default);
}