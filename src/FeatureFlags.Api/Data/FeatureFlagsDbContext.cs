using FeatureFlags.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FeatureFlags.Api.Data;

public sealed class FeatureFlagsDbContext(DbContextOptions<FeatureFlagsDbContext> options) : DbContext(options)
{
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<FeatureFilter> FeatureFilters => Set<FeatureFilter>();
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
            entity.HasMany(x => x.Filters)
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

        modelBuilder.Entity<FeatureStoreMeta>(entity =>
        {
            entity.ToTable("FeatureStoreMeta", schema);
            entity.HasKey(x => x.Id);
            entity.HasData(new FeatureStoreMeta { Id = 1, StoreVersion = 0 });
        });
    }
}
