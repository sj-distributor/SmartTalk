using System.Collections;
using SmartTalk.Messages.Constants;

namespace Smarties.Messages.DTO.RetrievalDb;

public class TagCollectionDto : IDictionary<string, List<string>>
{
    private readonly IDictionary<string, List<string>> _data = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

    public ICollection<string> Keys => _data.Keys;

    public ICollection<List<string>> Values => _data.Values;

    public IEnumerable<KeyValuePair<string, string>> Pairs =>
        from key in _data.Keys
        from value in _data[key]
        select new KeyValuePair<string, string>(key, value);

    public int Count => _data.Count;

    public bool IsReadOnly => _data.IsReadOnly;

    public List<string> this[string key]
    {
        get => _data[key];
        set
        {
            ValidateKey(key);
            _data[key] = value;
        }
    }

    public IEnumerator<KeyValuePair<string, List<string>>> GetEnumerator()
    {
        return _data.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Add(KeyValuePair<string, List<string>> item)
    {
        ValidateKey(item.Key);
        _data.Add(item);
    }

    public void Add(string key)
    {
        if (!_data.ContainsKey(key))
        {
            _data[key] = new List<string>();
        }
    }

    public void Add(string key, string value)
    {
        ValidateKey(key);
        if (_data.TryGetValue(key, out List<string> list) && list != null)
        {
            if (value != null) { list.Add(value); }
        }
        else
        {
            _data[key] = value == null ? new List<string>() : new List<string> { value };
        }
    }

    public void Add(string key, List<string> value)
    {
        ValidateKey(key);
        _data.Add(key, value);
    }

    public bool TryGetValue(string key, out List<string> value)
    {
        return _data.TryGetValue(key, out value);
    }

    public bool Contains(KeyValuePair<string, List<string>> item)
    {
        return _data.Contains(item);
    }

    public bool ContainsKey(string key)
    {
        return _data.ContainsKey(key);
    }

    public void CopyTo(KeyValuePair<string, List<string>>[] array, int arrayIndex)
    {
        _data.CopyTo(array, arrayIndex);
    }

    public void CopyTo(TagCollectionDto tagCollection)
    {
        foreach (var key in _data.Keys)
        {
            if (_data[key] == null || _data[key].Count == 0)
            {
                tagCollection.Add(key);
            }
            else
            {
                foreach (var value in _data[key])
                {
                    tagCollection.Add(key, value);
                }
            }
        }
    }

    public IEnumerable<KeyValuePair<string, string>> ToKeyValueList()
    {
        return (from tag in _data from tagValue in tag.Value select new KeyValuePair<string, string>(tag.Key, tagValue));
    }

    public bool Remove(KeyValuePair<string, List<string>> item)
    {
        return _data.Remove(item);
    }

    public bool Remove(string key)
    {
        return _data.Remove(key);
    }

    public void Clear()
    {
        _data.Clear();
    }

    private static void ValidateKey(string key)
    {
        if (key.Contains(VectorDbStore.ReservedEqualsChar))
        {
            throw new Exception($"A tag name cannot contain the '{VectorDbStore.ReservedEqualsChar}' char");
        }

        // '=' is reserved for backward/forward compatibility and to reduce URLs query params encoding complexity
        if (key.Contains('='))
        {
            throw new Exception("A tag name cannot contain the '=' char");
        }

        // ':' is reserved for backward/forward compatibility
        if (key.Contains(':'))
        {
            throw new Exception("A tag name cannot contain the ':' char");
        }
    }
}