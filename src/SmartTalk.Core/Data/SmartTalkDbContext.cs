using System.Reflection;
using SmartTalk.Core.Domain;
using SmartTalk.Core.Settings;
using SmartTalk.Messages.Constants;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data.Exceptions;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.Infrastructure;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Serilog;

namespace SmartTalk.Core.Data;

public class SmartTalkDbContext : DbContext, IUnitOfWork
{
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly SmartTalkConnectionString _connectionString;

    public SmartTalkDbContext(IClock clock, ICurrentUser currentUser, SmartTalkConnectionString connectionString)
    {
        _clock = clock;
        _currentUser = currentUser;
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
            TrackModification(entityEntry);
        }
        
        return await base.SaveChangesAsync(cancellationToken);
    }
    
    private void TrackCreated(EntityEntry entityEntry)
    {
        if (entityEntry.State == EntityState.Added && entityEntry.Entity is IHasCreatedFields createdEntity)
        {
            Log.Information("Tracking entity creation, entity: {@Entity}, current user: {@CurrentUser}", entityEntry.Entity, _currentUser);
            
            createdEntity.CreatedDate = _clock.Now;
        }
    }
    
    private void TrackModification(EntityEntry entityEntry)
    {
        if (entityEntry.Entity is IHasModifiedFields modifyEntity && entityEntry.State is EntityState.Modified or EntityState.Added)
        { 
            Log.Information("Tracking entity modification, entity: {@Entity}, current user: {@CurrentUser}", entityEntry.Entity, _currentUser);
            
            if (_currentUser is not { Id: not null } && modifyEntity.LastModifiedBy == 0) throw new MissingCurrentUserWhenSavingNonNullableFieldException(nameof(modifyEntity.LastModifiedBy));
            
            modifyEntity.LastModifiedDate = _clock.Now;
            
            if (_currentUser?.Id != null && (modifyEntity.LastModifiedBy == 0 || _currentUser.Id.Value != CurrentUsers.InternalUser.Id))
                modifyEntity.LastModifiedBy = _currentUser.Id.Value;
        }
    }
    
    public bool ShouldSaveChanges { get; set; }
}