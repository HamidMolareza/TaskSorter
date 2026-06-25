using Microsoft.EntityFrameworkCore;
using TaskSorter.Backend.Profiles;

namespace TaskSorter.Backend.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TaskProfile> TaskProfiles => Set<TaskProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskProfile>(entity =>
        {
            entity.HasKey(profile => profile.Id);
            entity.HasIndex(profile => profile.Name).IsUnique();
            entity.Property(profile => profile.Name).HasMaxLength(120).IsRequired();
            entity.Property(profile => profile.RepositoryLines).IsRequired();
            entity.Property(profile => profile.LabelLines).IsRequired();
            entity.Property(profile => profile.EncryptedGitHubToken).HasMaxLength(4096);
        });
    }
}
