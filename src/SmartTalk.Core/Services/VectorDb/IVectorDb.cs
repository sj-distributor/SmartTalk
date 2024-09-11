using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.VectorDb;
using SmartTalk.Messages.Enums.OpenAi;

namespace SmartTalk.Core.Services.RetrievalDb.VectorDb;

public interface IVectorDb : IScopedDependency
{
    Task CreateIndexAsync(string index, int vectorSize, CancellationToken cancellationToken = default);

    Task<IEnumerable<string>> GetIndexesAsync(CancellationToken cancellationToken = default);

    Task DeleteIndexAsync(string index, CancellationToken cancellationToken = default);

    Task<string> UpsertAsync(string index, VectorRecordDto record, CancellationToken cancellationToken = default);
    
    IAsyncEnumerable<(VectorRecordDto, double)> GetSimilarListAsync(
        string index, string text, ICollection<RetrievalFilterDto> filters = null, double minRelevance = 0, int limit = 1,
        bool withEmbeddings = false, OpenAiEmbeddingModel model = OpenAiEmbeddingModel.TextEmbedding3Large, CancellationToken cancellationToken = default);

    IAsyncEnumerable<VectorRecordDto> GetListAsync(
        string index, ICollection<RetrievalFilterDto> filters = null, int limit = 1, bool withEmbeddings = false, CancellationToken cancellationToken = default);

    Task DeleteAsync(string index, VectorRecordDto record, CancellationToken cancellationToken = default);
}