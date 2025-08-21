namespace SmartTalk.Messages.Dto.Google;

public class GoogleSchemaDto
{
    public GoogleSchemaType Type { get; set; }

    public string Format { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public bool? Nullable { get; set; }

    public List<string> EnumValues { get; set; }

    public long? MaxItems { get; set; }

    public long? MinItems { get; set; }

    public Dictionary<string, GoogleSchemaDto> Properties { get; set; }

    public List<string> Required { get; set; }

    public long? MinProperties { get; set; }

    public long? MaxProperties { get; set; }

    public long? MinLength { get; set; }

    public long? MaxLength { get; set; }

    public string Pattern { get; set; }

    public object Example { get; set; }

    public List<GoogleSchemaDto> AnyOf { get; set; }

    public List<string> PropertyOrdering { get; set; }

    public object Default { get; set; }

    public GoogleSchemaDto Items { get; set; }

    public double? Minimum { get; set; }

    public double? Maximum { get; set; }
}

public enum GoogleSchemaType
{
    OBJECT,
    ARRAY,
    STRING,
    INTEGER,
    NUMBER,
    BOOLEAN,
    NULL
}