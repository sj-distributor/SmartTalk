using System.Linq.Expressions;
using System.Reflection;
using SmartTalk.Core.Domain;
using SmartTalk.Core.Settings;
using SmartTalk.Messages.Constants;
using Microsoft.EntityFrameworkCore;
using SmartTalk.Core.Data.Exceptions;
using SmartTalk.Core.Services.Identity;
using SmartTalk.Core.Services.Infrastructure;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Messages.Enums.System;

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
    
    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<DayOfWeekSet>()
            .HaveConversion<DowSetValueConverter>()
            .HaveColumnType("SET('MON','TUE','WED','THU','FRI','SAT','SUN')");
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
            createdEntity.CreatedDate = _clock.Now;
        }
    }
    
    private void TrackModification(EntityEntry entityEntry)
    {
        if (entityEntry.Entity is IHasModifiedFields modifyEntity && entityEntry.State is EntityState.Modified or EntityState.Added)
        { 
            if (_currentUser is not { Id: not null } && modifyEntity.LastModifiedBy == 0) throw new MissingCurrentUserWhenSavingNonNullableFieldException(nameof(modifyEntity.LastModifiedBy));
            
            modifyEntity.LastModifiedDate = _clock.Now;
            
            if (_currentUser?.Id != null && (modifyEntity.LastModifiedBy == 0 || _currentUser.Id.Value != CurrentUsers.InternalUser.Id))
                modifyEntity.LastModifiedBy = _currentUser.Id.Value;
        }
    }
    
    public static class DowSetConverter
    {
        private static readonly DayOfWeekSet[] AllFlags =
            Enum.GetValues(typeof(DayOfWeekSet))
                .Cast<DayOfWeekSet>()
                .Where(x => x != DayOfWeekSet.None)
                .ToArray();

        public static string ToProvider(DayOfWeekSet v) =>
            v == DayOfWeekSet.None
                ? string.Empty
                : string.Join(",", AllFlags.Where(set => v.HasFlag(set)));

        public static DayOfWeekSet FromProvider(string v) =>
            string.IsNullOrEmpty(v)
                ? DayOfWeekSet.None
                : v.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Aggregate(DayOfWeekSet.None, (acc, s) => acc | Enum.Parse<DayOfWeekSet>(s));
    }
    
    public sealed class DowSetValueConverter() : ValueConverter<DayOfWeekSet, string>(
        (Expression<Func<DayOfWeekSet, string>>)(v => DowSetConverter.ToProvider(v)),
        (Expression<Func<string, DayOfWeekSet>>)(v => DowSetConverter.FromProvider(v)));
    
    public bool ShouldSaveChanges { get; set; }
}