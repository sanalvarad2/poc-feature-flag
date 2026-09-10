using FeatureFlags.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FeatureFlags.Api.Data;

public sealed class FeatureFlagsDbContext(DbContextOptions<FeatureFlagsDbContext> options) : DbContext(options)
{
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<FeatureFilter> FeatureFilters => Set<FeatureFilter>();
    public DbSet<FeatureVariant> FeatureVariants => Set<FeatureVariant>();
    public DbSet<FeatureAllocationUser> FeatureAllocationUsers => Set<FeatureAllocationUser>();
    public DbSet<FeatureAllocationGroup> FeatureAllocationGroups => Set<FeatureAllocationGroup>();
    public DbSet<FeatureAllocationPercentile> FeatureAllocationPercentiles => Set<FeatureAllocationPercentile>();
    public DbSet<FeatureStoreMeta> FeatureStoreMeta => Set<FeatureStoreMeta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        const string schema = "FeatureFlags";

        modelBuilder.Entity<FeatureFlag>(entity =>
        {
            entity.ToTable("FeatureFlags", schema);
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.RequirementType).HasMaxLength(16).IsRequired();
            entity.Property(x => x.DefaultWhenEnabled).HasMaxLength(256);
            entity.Property(x => x.DefaultWhenDisabled).HasMaxLength(256);
            entity.Property(x => x.AllocationSeed).HasMaxLength(256);
            entity.HasMany(x => x.Filters)
                .WithOne(x => x.FeatureFlag)
                .HasForeignKey(x => x.FeatureFlagId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Variants)
                .WithOne(x => x.FeatureFlag)
                .HasForeignKey(x => x.FeatureFlagId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.AllocationUsers)
                .WithOne(x => x.FeatureFlag)
                .HasForeignKey(x => x.FeatureFlagId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.AllocationGroups)
                .WithOne(x => x.FeatureFlag)
                .HasForeignKey(x => x.FeatureFlagId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.AllocationPercentiles)
                .WithOne(x => x.FeatureFlag)
                .HasForeignKey(x => x.FeatureFlagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FeatureFilter>(entity =>
        {
            entity.ToTable("FeatureFilters", schema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.ParametersJson).IsRequired();
        });

        modelBuilder.Entity<FeatureVariant>(entity =>
        {
            entity.ToTable("FeatureVariants", schema);
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.FeatureFlagId, x.Name }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.StatusOverride).HasMaxLength(16).IsRequired();
        });

        modelBuilder.Entity<FeatureAllocationUser>(entity =>
        {
            entity.ToTable("FeatureAllocationUsers", schema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.VariantName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.UserId).HasMaxLength(256).IsRequired();
            entity.HasIndex(x => new { x.FeatureFlagId, x.VariantName, x.UserId }).IsUnique();
        });

        modelBuilder.Entity<FeatureAllocationGroup>(entity =>
        {
            entity.ToTable("FeatureAllocationGroups", schema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.VariantName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.GroupName).HasMaxLength(256).IsRequired();
            entity.HasIndex(x => new { x.FeatureFlagId, x.VariantName, x.GroupName }).IsUnique();
        });

        modelBuilder.Entity<FeatureAllocationPercentile>(entity =>
        {
            entity.ToTable("FeatureAllocationPercentiles", schema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.VariantName).HasMaxLength(256).IsRequired();
        });

        modelBuilder.Entity<FeatureStoreMeta>(entity =>
        {
            entity.ToTable("FeatureStoreMeta", schema);
            entity.HasKey(x => x.Id);
            entity.HasData(new FeatureStoreMeta { Id = 1, StoreVersion = 0 });
        });
    }
}
