using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Autofac;
using NRedisStack;
using NRedisStack.RedisStackCommands;
using NRedisStack.Search;
using NRedisStack.Search.Literals.Enums;
using Serilog;
using Smarties.Core.Services.RetrievalDb.VectorDb;
using SmartTalk.Core.Services.Embedding;
using SmartTalk.Core.Services.RetrievalDb.VectorDb;
using SmartTalk.Core.Settings.VectorDb;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.Embedding;
using SmartTalk.Messages.Dto.VectorDb;
using SmartTalk.Messages.Enums.Caching;
using SmartTalk.Messages.Enums.OpenAi;
using StackExchange.Redis;

namespace SmartTalk.Core.Services.VectorDb;

public class RedisVector : IVectorDb
{
    private const string EmbeddingFieldName = "embedding";
    private const string PayloadFieldName = "payload";
    private const char DefaultSeparator = ',';
    private const string DistanceFieldName = $"__{EmbeddingFieldName}_score";
    
    private readonly IDatabase _db;
    private readonly ISearchCommandsAsync _search;
    private readonly VectorDbSettings _vectorDbSettings;
    private readonly ITextEmbeddingGenerator _embeddingGenerator;
    private static readonly Regex s_replaceIndexNameCharsRegex = new(@"[\s|\\|/|.|_|:]");
    private readonly string[] _fieldNamesNoEmbeddings;
    private const string KmSeparator = "-";
    private static readonly char[] s_tagEscapeChars =
    {
        ',', '.', '<', '>', '{', '}', '[', ']', '"', '\'', ':', ';',
        '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', '-', '+', '=', '~', '|', ' ', '/',
    };
    
    public RedisVector(IComponentContext context, VectorDbSettings vectorDbSettings, ITextEmbeddingGenerator embeddingGenerator)
    {
        var vectorRedis = context.ResolveKeyed<ConnectionMultiplexer>(RedisServer.Vector);
        _search = vectorRedis.GetDatabase().FT();
        _db = vectorRedis.GetDatabase();
        _vectorDbSettings = vectorDbSettings;
        _embeddingGenerator = embeddingGenerator;
    }
    
    public async Task CreateIndexAsync(string index, int vectorSize, CancellationToken cancellationToken = default)
    {
        var normalizedIndexName = NormalizeIndexName(index, _vectorDbSettings.AppPrefix);
        var schema = new Schema().AddVectorField(
            EmbeddingFieldName, Enum.Parse<Schema.VectorField.VectorAlgo>(VectorDbStore.VectorAlgorithm, true), new Dictionary<string, object>
            {
                { "TYPE", "FLOAT32" },
                { "DIM", vectorSize },
                { "DISTANCE_METRIC", "COSINE" }
            });

        var ftParams = new FTCreateParams().On(IndexDataType.HASH).Prefix($"{normalizedIndexName}:");

        foreach (var tag in VectorDbStore.Tags)
        {
            var fieldName = tag.Key;
            var separator = tag.Value ?? DefaultSeparator;
            schema.AddTagField(fieldName, separator: separator.ToString());
        }

        try
        {
            await _search.CreateAsync(normalizedIndexName, ftParams, schema).ConfigureAwait(false);
        }
        catch (RedisServerException ex)
        {
            if (!ex.Message.Contains("Index already exists", StringComparison.OrdinalIgnoreCase))
            {
                throw;
            }
        }
    }

    public async Task<IEnumerable<string>> GetIndexesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _search._ListAsync().ConfigureAwait(false);
        return result.Select(x => ((string)x!)).Where(x => x.StartsWith($"{_vectorDbSettings.AppPrefix}", StringComparison.Ordinal)).Select(x => x.Substring(_vectorDbSettings.AppPrefix.Length));
    }

    public async Task DeleteIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        var normalizedIndexName = NormalizeIndexName(index, _vectorDbSettings.AppPrefix);
        try
        {
            // we are explicitly dropping all records associated with the index here.
            await _search.DropIndexAsync(normalizedIndexName, dd: true).ConfigureAwait(false);
        }
        catch (RedisServerException exception)
        {
            if (!exception.Message.Equals("unknown index name", StringComparison.OrdinalIgnoreCase))
            {
                throw;
            }
        }
    }

    public async Task<string> UpsertAsync(string index, VectorRecordDto record, CancellationToken cancellationToken = default)
    {
        var normalizedIndexName = NormalizeIndexName(index, _vectorDbSettings.AppPrefix);
        var key = Key(normalizedIndexName, record.Id);
        var args = new List<RedisValue>
        {
            NormalizeIndexName(index, _vectorDbSettings.AppPrefix),
            EmbeddingFieldName,
            record.Vector.VectorBlob()
        };

        foreach (var item in record.Tags)
        {
            var isIndexed = VectorDbStore.Tags.TryGetValue(item.Key, out var c);
            var separator = c ?? DefaultSeparator;
            if (!isIndexed)
                throw new ArgumentException($"Attempt to insert un-indexed tag field: '{item.Key}', will not be able to filter on it, please adjust the tag settings in your Redis Configuration");

            if (item.Value.Any(s => s is not null && s.Contains(separator.ToString(), StringComparison.InvariantCulture)))
            {
                throw new ArgumentException($"Attempted to insert record with tag field: '{item.Key}' containing the separator: '{separator}'. " +
                                            $"Update your {nameof(VectorDbSettings)} to use a different separator, or remove the separator from the field.");
            }

            args.Add(item.Key);
            args.Add(string.Join(separator, item.Value));
        }

        if (record.Payload.Count != 0)
        {
            args.Add(PayloadFieldName);
            args.Add(JsonSerializer.Serialize(record.Payload));
        }

        var scriptResult = (await _db.ScriptEvaluateAsync(Scripts.CheckIndexAndUpsert, new RedisKey[] { key }, args.ToArray()).ConfigureAwait(false)).ToString()!;
        Log.Information("Upsert redis script execute result: {@ScriptResult}\nkey:{@Key}\n{@Args},", scriptResult, key, args.ToArray());

        if (scriptResult == "false")
        {
            await CreateIndexAsync(index, record.Vector.Length, cancellationToken).ConfigureAwait(false);
            await _db.ScriptEvaluateAsync(Scripts.CheckIndexAndUpsert, new RedisKey[] { key }, args.ToArray()).ConfigureAwait(false);
        }
        else if (scriptResult.StartsWith("(error)", StringComparison.Ordinal))
        {
            throw new RedisException(scriptResult);
        }

        return record.Id;
    }
    
    public async IAsyncEnumerable<(VectorRecordDto, double)> GetSimilarListAsync(
        string index, string text, ICollection<RetrievalFilterDto> filters = null, double minRelevance = 0, int limit = 1, 
        bool withEmbeddings = false, OpenAiEmbeddingModel model = OpenAiEmbeddingModel.TextEmbedding3Large, CancellationToken cancellationToken = default)
    {
        var normalizedIndexName = NormalizeIndexName(index, _vectorDbSettings.AppPrefix);
        var embedding = await _embeddingGenerator.GenerateEmbeddingAsync(text, model, cancellationToken: cancellationToken).ConfigureAwait(false);
        Log.Information("Generating embedding: {@Parameters}", embedding);
        var blob = embedding.VectorBlob();
        var parameters = new Dictionary<string, object>
        {
            { "blob", blob },
            { "limit", limit }
        };

        Log.Information("Getting similar list: {@Parameters}", parameters);

        var sb = new StringBuilder();
        if (filters != null && filters.Any(x => x.Pairs.Any()))
        {
            sb.Append('(');
            foreach (var filter in filters)
            {
                sb.Append('(');
                foreach ((string key, string? value) in filter.Pairs)
                {
                    if (value is null)
                    {
                        throw new RedisException("Attempted to perform null check on tag field. This behavior is not supported by Redis");
                    }

                    sb.Append(CultureInfo.InvariantCulture, $"@{key}:{{{value}}} ");
                }

                sb.Replace(" ", ")|", sb.Length - 1, 1);
            }

            sb.Replace('|', ')', sb.Length - 1, 1);
        }
        else
        {
            sb.Append('*');
        }

        sb.Append($"=>[KNN $limit @{EmbeddingFieldName} $blob]");

        var query = new Query(sb.ToString());
        Log.Information("Get similar list query: {@Query}", sb.ToString());
        
        query.Params(parameters);
        query.Limit(0, limit);
        query.Dialect(2);
        query.SortBy = DistanceFieldName;
        if (!withEmbeddings)
        {
            query.ReturnFields(_fieldNamesNoEmbeddings);
        }

        SearchResult result = null;

        try
        {
            result = await _search.SearchAsync(normalizedIndexName, query).ConfigureAwait(false);
            Log.Information("Get similar list result: {@Result}", result);
        }
        catch (RedisServerException e)
        {
            if (!e.Message.Contains("no such index", StringComparison.OrdinalIgnoreCase))
            {
                throw;
            }
        }

        if (result is null)
        {
            yield break;
        }

        foreach (var doc in result.Documents)
        {
            var next = FromDocument(doc, withEmbeddings);
            
            Log.Information("Get similar list result: {@Next}", next);
            
            if (next.Item2 > minRelevance)
            {
                yield return next;
            }
        }
    }

    public async IAsyncEnumerable<VectorRecordDto> GetListAsync(string index, ICollection<RetrievalFilterDto> filters = null, int limit = 1, bool withEmbeddings = false, CancellationToken cancellationToken = default)
    {
        var normalizedIndexName = NormalizeIndexName(index, _vectorDbSettings.AppPrefix);
        var sb = new StringBuilder();
        if (filters != null && filters.Any(x => x.Pairs.Any()))
        {
            foreach ((string key, string value) in filters.SelectMany(x => x.Pairs))
            {
                if (value is null)
                {
                    Log.Warning("Attempted to perform null check on tag field. This behavior is not supported by Redis");
                }

                sb.Append(CultureInfo.InvariantCulture, $" @{key}:{{{EscapeTagField(value!)}}}");
            }
        }
        else
        {
            sb.Append('*');
        }

        var query = new Query(sb.ToString());
        if (!withEmbeddings)
        {
            query.ReturnFields(_fieldNamesNoEmbeddings);
        }

        List<Document> documents = new();
        try
        {
            // handle the case of negative indexes (-1 = end, -2 = 1 from end, etc. . .)
            if (limit < 0)
            {
                var numOfDocumentsFromEnd = -1 * (limit + 1);

                var probingQueryTask = _search.SearchAsync(normalizedIndexName, query);
                var configurationCheckTask = _search.ConfigGetAsync("MAXSEARCHRESULTS"); // need to query Max Search Results since Redis doesn't support unbounded queries.

                // pull back in one round trip, hence the separated awaits.
                var firstTripDocs = await probingQueryTask.ConfigureAwait(false);
                var configurationResult = await configurationCheckTask.ConfigureAwait(false);

                var docsNeeded = (int)firstTripDocs.TotalResults - numOfDocumentsFromEnd;

                documents.AddRange(firstTripDocs.Documents.Take(docsNeeded));
                if (docsNeeded > 10)
                {
                    if (configurationResult.TryGetValue("MAXSEARCHRESULTS", out string? value) && int.TryParse(value, out var maxSearchResults))
                    {
                        limit = Math.Min(docsNeeded - 10, maxSearchResults);
                        query.Limit(10, limit);
                        var secondTripResults = await _search.SearchAsync(normalizedIndexName, query).ConfigureAwait(false);
                        documents.AddRange(secondTripResults.Documents);
                    }
                    else // shouldn't be reachable.
                    {
                        throw new RedisException("Redis does not contain a valid value for MAXSEARCHRESULTS, possible configuration issue in Redis.");
                    }
                }
            }
            else
            {
                query.Limit(0, limit);
                var result = await _search.SearchAsync(normalizedIndexName, query).ConfigureAwait(false);
                documents.AddRange(result.Documents);
            }
        }
        catch (RedisServerException ex) when (ex.Message.Contains("no such index", StringComparison.InvariantCulture))
        {
            // NOOP
        }

        foreach (var doc in documents)
        {
            yield return FromDocument(doc, withEmbeddings).Item1;
        }
    }

    public async Task DeleteAsync(string index, VectorRecordDto record, CancellationToken cancellationToken = default)
    {
        var normalizedIndexName = NormalizeIndexName(index, _vectorDbSettings.AppPrefix);
        var key = Key(normalizedIndexName, record.Id);
        await _db.KeyDeleteAsync(key);
    }
    
    private static string NormalizeIndexName(string index, string prefix = null)
    {
        if (string.IsNullOrWhiteSpace(index))
        {
            throw new ArgumentNullException(nameof(index), "The index name is empty");
        }

        var indexWithPrefix = !string.IsNullOrWhiteSpace(prefix) ? $"{prefix}{index}" : index;

        indexWithPrefix = s_replaceIndexNameCharsRegex.Replace(indexWithPrefix.Trim().ToLowerInvariant(), KmSeparator);

        return indexWithPrefix;
    }
    
    private (VectorRecordDto, double) FromDocument(Document doc, bool withEmbedding)
    {
        Log.Information("Get similar list result Document: {@doc}, withEmbedding {@withEmbedding}", doc, withEmbedding);
        
        double distance = 0;
        var memoryRecord = new VectorRecordDto
        {
            Id = doc.Id.Split(":", 2)[1]
        };

        foreach (var field in doc.GetProperties())
        {
            Log.Information("Get similar list result FromDocument: {@field}", field);
            if (field.Key == EmbeddingFieldName)
            {
                if (withEmbedding)
                {
                    var floats = ByteArrayToFloatArray((byte[])field.Value!);
                    memoryRecord.Vector = new EmbeddingDto(floats);
                }
            }
            else if (field.Key == PayloadFieldName)
            {
                var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(field.Value.ToString());
                memoryRecord.Payload = payload ?? new Dictionary<string, object>();
            }
            else if (field.Key == DistanceFieldName)
            {
                distance = 1 - (double)field.Value;
            }
            else
            {
                VectorDbStore.Tags.TryGetValue(field.Key, out var c);
                var separator = c ?? DefaultSeparator;
                var values = ((string)field.Value!)?.Split(separator);
                memoryRecord.Tags.Add(new KeyValuePair<string, List<string?>>(field.Key, new List<string?>(values)));
            }
        }

        return (memoryRecord, distance);
    }

    private static string EscapeTagField(string text)
    {
        var sb = new StringBuilder();
        foreach (var c in text)
        {
            if (s_tagEscapeChars.Contains(c))
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static RedisKey Key(string indexWithPrefix, string id) => $"{indexWithPrefix}:{id}";

    private static float[] ByteArrayToFloatArray(byte[] bytes)
    {
        if (bytes.Length % 4 != 0)
        {
            throw new InvalidOperationException("Encountered an unbalanced array of bytes for float array conversion");
        }

        var res = new float[bytes.Length / 4];
        for (int i = 0; i < bytes.Length / 4; i++)
        {
            res[i] = BitConverter.ToSingle(bytes, i * 4);
        }

        return res;
    }
}