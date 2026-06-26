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
    }
}
