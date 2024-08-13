namespace SmartTalk.Core.Domain;

public interface IHasCreatedFields
{
    DateTimeOffset CreatedDate { get; set; }
}