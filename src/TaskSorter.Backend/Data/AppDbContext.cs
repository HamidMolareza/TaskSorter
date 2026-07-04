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
    public DbSet<RepositoryPriorityFactor> RepositoryPriorityFactors => Set<RepositoryPriorityFactor>();
    public DbSet<RepositoryFactorRating> RepositoryFactorRatings => Set<RepositoryFactorRating>();

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
            entity.Property(tier => tier.RowVersion).IsConcurrencyToken();
        });

        modelBuilder.Entity<ProfileRepository>(entity =>
        {
            entity.HasKey(repository => repository.Id);
            entity.HasIndex(repository => new { repository.ProfileId, repository.Owner, repository.Name }).IsUnique();
            entity.HasIndex(repository => new { repository.ProfileId, repository.SortOrder }).IsUnique();
            entity.Property(repository => repository.Owner).HasMaxLength(100).IsRequired();
            entity.Property(repository => repository.Name).HasMaxLength(100).IsRequired();
            entity.Property(repository => repository.RowVersion).IsConcurrencyToken();
            entity.HasOne(repository => repository.Profile)
                .WithMany(profile => profile.Repositories)
                .HasForeignKey(repository => repository.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(repository => repository.RepositoryTier)
                .WithMany(tier => tier.ProfileRepositories)
                .HasForeignKey(repository => repository.RepositoryTierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RepositoryPriorityFactor>(entity =>
        {
            entity.HasKey(factor => factor.Id);
            entity.HasIndex(factor => new { factor.ProfileId, factor.NormalizedName }).IsUnique();
            entity.HasIndex(factor => new { factor.ProfileId, factor.SortOrder }).IsUnique();
            entity.Property(factor => factor.Name).HasMaxLength(80).IsRequired();
            entity.Property(factor => factor.NormalizedName).HasMaxLength(80).IsRequired();
            entity.Property(factor => factor.Description).HasMaxLength(500).IsRequired();
            entity.Property(factor => factor.RowVersion).IsConcurrencyToken();
            entity.HasOne(factor => factor.Profile)
                .WithMany(profile => profile.RepositoryPriorityFactors)
                .HasForeignKey(factor => factor.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RepositoryFactorRating>(entity =>
        {
            entity.HasKey(rating => new { rating.ProfileRepositoryId, rating.RepositoryPriorityFactorId });
            entity.HasIndex(rating => rating.RepositoryPriorityFactorId);
            entity.Property(rating => rating.RowVersion).IsConcurrencyToken();
            entity.HasOne(rating => rating.ProfileRepository)
                .WithMany(repository => repository.FactorRatings)
                .HasForeignKey(rating => rating.ProfileRepositoryId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(rating => rating.RepositoryPriorityFactor)
                .WithMany(factor => factor.Ratings)
                .HasForeignKey(rating => rating.RepositoryPriorityFactorId)
                .OnDelete(DeleteBehavior.Cascade);
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
