using System.Reflection;
using SmartTalk.Core.Domain;
using SmartTalk.Core.Settings;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Services.Infrastructure;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace SmartTalk.Core.Data;

public class SmartTalkDbContext : DbContext, IUnitOfWork
{
    private readonly IClock _clock;
    private readonly SmartTalkConnectionString _connectionString;

    public SmartTalkDbContext(IClock clock, SmartTalkConnectionString connectionString)
    {
        _clock = clock;
        _connectionString = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseMySql(_connectionString.Value, new MySqlServerVersion(new Version(8, 0, 28)));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        typeof(SmartTalkDbContext).GetTypeInfo().Assembly.GetTypes()
            .Where(t => typeof(IEntity).IsAssignableFrom(t) && t.IsClass).ToList()
            .ForEach(x =>
            {
                if (modelBuilder.Model.FindEntityType(x) == null)
                    modelBuilder.Model.AddEntityType(x);
            });
    }
    
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        
        foreach (var entityEntry in ChangeTracker.Entries())
        {
            TrackCreated(entityEntry);
        }
        
        return await base.SaveChangesAsync(cancellationToken);
    }
    
    private void TrackCreated(EntityEntry entityEntry)
    {
        if (entityEntry.State == EntityState.Added && entityEntry.Entity is IHasCreatedFields createdEntity)
        {
            createdEntity.CreatedDate = _clock.Now;
        }
    }
    
    public bool ShouldSaveChanges { get; set; }
}