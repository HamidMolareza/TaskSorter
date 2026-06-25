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
