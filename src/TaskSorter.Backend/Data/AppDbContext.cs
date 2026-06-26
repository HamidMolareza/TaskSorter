using Microsoft.EntityFrameworkCore;
using TaskSorter.Backend.Profiles;

namespace TaskSorter.Backend.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TaskProfile> TaskProfiles => Set<TaskProfile>();
    public DbSet<GitHubCacheEntry> GitHubCacheEntries => Set<GitHubCacheEntry>();
    public DbSet<GitHubQuotaSnapshotEntry> GitHubQuotaSnapshots => Set<GitHubQuotaSnapshotEntry>();
    public DbSet<RepositoryTier> RepositoryTiers => Set<RepositoryTier>();
    public DbSet<ProfileRepository> ProfileRepositories => Set<ProfileRepository>();
    public DbSet<ProfileLabel> ProfileLabels => Set<ProfileLabel>();

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

        modelBuilder.Entity<RepositoryTier>(entity =>
        {
            entity.HasKey(tier => tier.Id);
            entity.HasIndex(tier => tier.NormalizedName).IsUnique();
            entity.HasIndex(tier => tier.IsDefault).IsUnique().HasFilter("\"IsDefault\" = true");
            entity.Property(tier => tier.Name).HasMaxLength(80).IsRequired();
            entity.Property(tier => tier.NormalizedName).HasMaxLength(80).IsRequired();
        });

        modelBuilder.Entity<ProfileRepository>(entity =>
        {
            entity.HasKey(repository => repository.Id);
            entity.HasIndex(repository => new { repository.ProfileId, repository.Owner, repository.Name }).IsUnique();
            entity.HasIndex(repository => new { repository.ProfileId, repository.SortOrder }).IsUnique();
            entity.Property(repository => repository.Owner).HasMaxLength(100).IsRequired();
            entity.Property(repository => repository.Name).HasMaxLength(100).IsRequired();
            entity.HasOne(repository => repository.Profile)
                .WithMany(profile => profile.Repositories)
                .HasForeignKey(repository => repository.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(repository => repository.RepositoryTier)
                .WithMany(tier => tier.ProfileRepositories)
                .HasForeignKey(repository => repository.RepositoryTierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProfileLabel>(entity =>
        {
            entity.HasKey(label => label.Id);
            entity.HasIndex(label => new { label.ProfileId, label.NormalizedName }).IsUnique();
            entity.HasIndex(label => new { label.ProfileId, label.SortOrder })
                .IsUnique()
                .HasFilter("\"SortOrder\" IS NOT NULL");
            entity.Property(label => label.Name).HasMaxLength(200).IsRequired();
            entity.Property(label => label.NormalizedName).HasMaxLength(200).IsRequired();
            entity.HasOne(label => label.Profile)
                .WithMany(profile => profile.Labels)
                .HasForeignKey(label => label.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
