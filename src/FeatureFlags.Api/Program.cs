using FeatureFlags.Api.Data;
using FeatureFlags.Api.Endpoints;
using FeatureFlags.Api.FeatureManagement;
using FeatureFlags.Api.Options;
using FeatureFlags.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "FeatureFlags API",
        Version = "v1",
        Description = "POC feature flag administration and evaluation. No authentication."
    });
});
builder.Services.Configure<FeatureStoreOptions>(
    builder.Configuration.GetSection(FeatureStoreOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("FeatureStore")
    ?? throw new InvalidOperationException("Connection string 'FeatureStore' is not configured.");

var databaseProvider = builder.Configuration["FeatureStore:DatabaseProvider"] ?? "SqlServer";

builder.Services.AddDbContext<FeatureFlagsDbContext>(options =>
{
    if (string.Equals(databaseProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<FeatureAdminService>();
builder.Services.AddSingleton<SqlFeatureDefinitionProvider>();
builder.Services.AddSingleton<IFeatureDefinitionProvider>(sp =>
    sp.GetRequiredService<SqlFeatureDefinitionProvider>());
builder.Services.AddSingleton<ITargetingContextAccessor, HttpHeaderTargetingContextAccessor>();
builder.Services.AddHostedService<FeatureStoreVersionWatcher>();

builder.Services
    .AddFeatureManagement()
    .AddFeatureFilter<PercentageFilter>()
    .AddFeatureFilter<TimeWindowFilter>()
    .AddFeatureFilter<TargetingFilter>()
    .WithTargeting();

var app = builder.Build();

// POC: Swagger available in all environments so endpoints are easy to try.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "FeatureFlags API v1");
    options.RoutePrefix = "swagger";
});

var applyMigrations = builder.Configuration.GetValue("FeatureStore:ApplyMigrations", true);
if (applyMigrations)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FeatureFlagsDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

    if (string.Equals(databaseProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        await db.Database.EnsureCreatedAsync();
        logger.LogInformation("SQLite database ensured at configured connection.");
    }
    else
    {
        await DatabaseInitializer.EnsureSqlServerDatabaseExistsAsync(connectionString);
        logger.LogInformation("Ensured SQL Server database exists (created if missing).");
        await db.Database.MigrateAsync();
        logger.LogInformation("Applied EF Core migrations.");
    }
}

app.MapControllers();
app.MapFeatureEndpoints();

app.Run();

public partial class Program;
