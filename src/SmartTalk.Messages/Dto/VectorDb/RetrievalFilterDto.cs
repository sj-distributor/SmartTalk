using SmartTalk.Messages.Constants;

namespace SmartTalk.Messages.Dto.VectorDb;

public class RetrievalFilterDto : TagCollectionDto
{
    public bool IsEmpty()
    {
        return Count == 0;
    }

    public RetrievalFilterDto ByTag(string name, string value)
    {
        Add(name, value);
        return this;
    }

    public RetrievalFilterDto ByDocument(string docId)
    {
        Add(VectorDbStore.ReservedDocumentIdTag, docId);
        return this;
    }

    public IEnumerable<KeyValuePair<string, string>> GetFilters()
    {
        return ToKeyValueList();
    }
}

public static class RetrievalFiltersDto
{
    public static RetrievalFilterDto ByTag(string name, string value)
    {
        return new RetrievalFilterDto().ByTag(name, value);
    }

    public static RetrievalFilterDto ByDocument(string docId)
    {
        return new RetrievalFilterDto().ByDocument(docId);
    }
}