using Microsoft.EntityFrameworkCore;
using TaskSorter.Backend.Profiles;

namespace TaskSorter.Backend.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TaskProfile> TaskProfiles => Set<TaskProfile>();
    public DbSet<GitHubCacheEntry> GitHubCacheEntries => Set<GitHubCacheEntry>();
    public DbSet<GitHubQuotaSnapshotEntry> GitHubQuotaSnapshots => Set<GitHubQuotaSnapshotEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GitHubQuotaSnapshotEntry>(entity =>
        {
            entity.HasKey(entry => new { entry.TokenFingerprint, entry.Resource });
            entity.HasIndex(entry => entry.CapturedAt);
            entity.HasIndex(entry => entry.ResetAt);
            entity.Property(entry => entry.TokenFingerprint).HasMaxLength(64).IsRequired();
            entity.Property(entry => entry.Resource).HasMaxLength(64).IsRequired();
            entity.Property(entry => entry.Source).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<GitHubCacheEntry>(entity =>
        {
            entity.HasKey(entry => entry.Key);
            entity.HasIndex(entry => entry.ExpiresAt);
            entity.Property(entry => entry.Key).HasMaxLength(512).IsRequired();
            entity.Property(entry => entry.Operation).HasMaxLength(120).IsRequired();
            entity.Property(entry => entry.Target).HasMaxLength(512).IsRequired();
            entity.Property(entry => entry.ValueJson).IsRequired();
        });

        modelBuilder.Entity<TaskProfile>(entity =>
        {
            entity.HasKey(profile => profile.Id);
            entity.HasIndex(profile => profile.Name).IsUnique();
            entity.Property(profile => profile.Name).HasMaxLength(120).IsRequired();
            entity.Property(profile => profile.RepositoryLines).IsRequired();
            entity.Property(profile => profile.LabelLines).IsRequired();
            entity.Property(profile => profile.PriorityFactorsJson).HasColumnType("text");
            entity.Property(profile => profile.EncryptedGitHubToken).HasMaxLength(4096);
        });
    }
}
