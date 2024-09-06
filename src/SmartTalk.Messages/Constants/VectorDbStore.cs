namespace SmartTalk.Messages.Constants;

public class VectorDbStore
{
    // smart talk
    public const string ReservedTagsPrefix = "__";
    public const string ReservedEqualsChar = ";";

    public const string ReservedChatIdTag = $"{ReservedTagsPrefix}chat_id";
    public const string ReservedDocumentIdTag = $"{ReservedTagsPrefix}document_id";
    public const string ReservedContentUuidTag = $"{ReservedTagsPrefix}content_uuid";
    public const string ReservedParentContentUuidTag = $"{ReservedTagsPrefix}parent_content_uuid";
    public const string ReservedFilePartitionTag = $"{ReservedTagsPrefix}file_part";
    public const string ReservedFilePartitionNumberTag = $"{ReservedTagsPrefix}part_n";
    public const string ReservedFileSectionNumberTag = $"{ReservedTagsPrefix}sect_n";
    public const string ReservedFileTypeTag = $"{ReservedTagsPrefix}file_type";
    
    public const string ReservedFileSummarizedTag = $"{ReservedTagsPrefix}summarized";
    public const string ReservedFileContentTypeTag = $"{ReservedTagsPrefix}content_type";
    public const string ReservedFileContentSubTypeTag = $"{ReservedTagsPrefix}content_sub_type";
    
    public const string ReservedPayloadContentField = $"{ReservedTagsPrefix}content";
    
    public const string VectorAlgorithm = "HNSW";

    public static readonly Dictionary<string, char?> Tags = new()
    {
        { ReservedChatIdTag, '|' },
        { ReservedDocumentIdTag, '|' },
        { ReservedContentUuidTag, '|' },
        { ReservedParentContentUuidTag, '|' },
        { ReservedFilePartitionTag, '|' },
        { ReservedFileSectionNumberTag, '|' },
        { ReservedFilePartitionNumberTag, '|' },
        { ReservedFileTypeTag, '|' },
        { ReservedFileSummarizedTag, '|' },
        { ReservedFileContentTypeTag, '|' },
        { ReservedFileContentSubTypeTag, '|' }
    };
}