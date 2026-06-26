using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TaskSorter.Backend.Data;

#nullable disable

namespace TaskSorter.Backend.Migrations;

[DbContext(typeof(AppDbContext))]
partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity("TaskSorter.Backend.Data.GitHubCacheEntry", b =>
        {
            b.Property<string>("Key")
                .HasMaxLength(512)
                .HasColumnType("character varying(512)");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<DateTimeOffset>("ExpiresAt")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("Operation")
                .IsRequired()
                .HasMaxLength(120)
                .HasColumnType("character varying(120)");

            b.Property<string>("Target")
                .IsRequired()
                .HasMaxLength(512)
                .HasColumnType("character varying(512)");

            b.Property<DateTimeOffset>("UpdatedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("ValueJson")
                .IsRequired()
                .HasColumnType("text");

            b.HasKey("Key");

            b.HasIndex("ExpiresAt");

            b.ToTable("GitHubCacheEntries");
        });

        modelBuilder.Entity("TaskSorter.Backend.Data.GitHubQuotaSnapshotEntry", b =>
        {
            b.Property<string>("TokenFingerprint")
                .HasMaxLength(64)
                .HasColumnType("character varying(64)");

            b.Property<string>("Resource")
                .HasMaxLength(64)
                .HasColumnType("character varying(64)");

            b.Property<DateTimeOffset>("CapturedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<int>("Limit")
                .HasColumnType("integer");

            b.Property<int>("Remaining")
                .HasColumnType("integer");

            b.Property<DateTimeOffset>("ResetAt")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("Source")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("character varying(64)");

            b.Property<int>("Used")
                .HasColumnType("integer");

            b.HasKey("TokenFingerprint", "Resource");

            b.HasIndex("CapturedAt");

            b.HasIndex("ResetAt");

            b.ToTable("GitHubQuotaSnapshots");
        });

        modelBuilder.Entity("TaskSorter.Backend.Profiles.ProfileRepository", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("Name").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<string>("Owner").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<Guid>("ProfileId").HasColumnType("uuid");
            b.Property<Guid>("RepositoryTierId").HasColumnType("uuid");
            b.Property<int>("SortOrder").HasColumnType("integer");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("ProfileId", "Owner", "Name").IsUnique();
            b.HasIndex("ProfileId", "SortOrder").IsUnique();
            b.HasIndex("RepositoryTierId");
            b.ToTable("ProfileRepositories");
        });

        modelBuilder.Entity("TaskSorter.Backend.Profiles.ProfileLabel", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<DateTimeOffset>("FirstDiscoveredAt").HasColumnType("timestamp with time zone");
            b.Property<bool>("IsIgnored").HasColumnType("boolean");
            b.Property<DateTimeOffset?>("LastDiscoveredAt").HasColumnType("timestamp with time zone");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<string>("NormalizedName").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<Guid>("ProfileId").HasColumnType("uuid");
            b.Property<int?>("SortOrder").HasColumnType("integer");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("ProfileId", "NormalizedName").IsUnique();
            b.HasIndex("ProfileId", "SortOrder").IsUnique().HasFilter("\"SortOrder\" IS NOT NULL");
            b.ToTable("ProfileLabels");
        });

        modelBuilder.Entity("TaskSorter.Backend.Profiles.RepositoryTier", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<bool>("IsDefault").HasColumnType("boolean");
            b.Property<string>("Name").IsRequired().HasMaxLength(80).HasColumnType("character varying(80)");
            b.Property<string>("NormalizedName").IsRequired().HasMaxLength(80).HasColumnType("character varying(80)");
            b.Property<int>("Score").HasColumnType("integer");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("IsDefault").IsUnique().HasFilter("\"IsDefault\" = true");
            b.HasIndex("NormalizedName").IsUnique();
            b.ToTable("RepositoryTiers");
        });

        modelBuilder.Entity("TaskSorter.Backend.Profiles.TaskProfile", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uuid");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<int>("DelayInMilliseconds")
                .HasColumnType("integer");

            b.Property<string>("EncryptedGitHubToken")
                .IsRequired()
                .HasMaxLength(4096)
                .HasColumnType("character varying(4096)");

            b.Property<string>("LabelLines")
                .IsRequired()
                .HasColumnType("text");

            b.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(120)
                .HasColumnType("character varying(120)");

            b.Property<string>("PriorityFactorsJson")
                .HasColumnType("text");

            b.Property<string>("RepositoryLines")
                .IsRequired()
                .HasColumnType("text");

            b.Property<int>("TaskLimit")
                .HasColumnType("integer");

            b.Property<DateTimeOffset>("UpdatedAt")
                .HasColumnType("timestamp with time zone");

            b.HasKey("Id");

            b.HasIndex("Name")
                .IsUnique();

            b.ToTable("TaskProfiles");
        });

        modelBuilder.Entity("TaskSorter.Backend.Profiles.ProfileRepository", b =>
        {
            b.HasOne("TaskSorter.Backend.Profiles.TaskProfile", "Profile")
                .WithMany("Repositories")
                .HasForeignKey("ProfileId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            b.HasOne("TaskSorter.Backend.Profiles.RepositoryTier", "RepositoryTier")
                .WithMany("ProfileRepositories")
                .HasForeignKey("RepositoryTierId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
        });

        modelBuilder.Entity("TaskSorter.Backend.Profiles.ProfileLabel", b =>
        {
            b.HasOne("TaskSorter.Backend.Profiles.TaskProfile", "Profile")
                .WithMany("Labels")
                .HasForeignKey("ProfileId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });
    }
}
