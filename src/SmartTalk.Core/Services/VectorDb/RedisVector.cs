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
using SmartTalk.Core.Settings.VectorDb;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.Embedding;
using SmartTalk.Messages.Dto.RetrievalDb.VectorDb;
using SmartTalk.Messages.Enums.Caching;
using StackExchange.Redis;

namespace SmartTalk.Core.Services.RetrievalDb.VectorDb;

public class RedisVector : IVectorDb
{
    private const string EmbeddingFieldName = "embedding";
    private const string PayloadFieldName = "payload";
    private const char DefaultSeparator = ',';
    private const string DistanceFieldName = $"__{EmbeddingFieldName}_score";
    
    private readonly IDatabase _db;
    private readonly ISearchCommandsAsync _search;
    private readonly VectorDbSettings _vectorDbSettings;
    private static readonly Regex s_replaceIndexNameCharsRegex = new(@"[\s|\\|/|.|_|:]");
    private const string KmSeparator = "-";
    private static readonly char[] s_tagEscapeChars =
    {
        ',', '.', '<', '>', '{', '}', '[', ']', '"', '\'', ':', ';',
        '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', '-', '+', '=', '~', '|', ' ', '/',
    };
    
    public RedisVector(IComponentContext context, VectorDbSettings vectorDbSettings)
    {
        var vectorRedis = context.ResolveKeyed<ConnectionMultiplexer>(RedisServer.Vector);
        _search = vectorRedis.GetDatabase().FT();
        _db = vectorRedis.GetDatabase();
        _vectorDbSettings = vectorDbSettings;
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