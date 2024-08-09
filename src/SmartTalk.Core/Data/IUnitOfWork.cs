namespace SmartTalk.Core.Data;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    bool ShouldSaveChanges { get; set; }
}