using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;

namespace CnabApi.Data;

/// <summary>
/// Design-time factory for Entity Framework Core migrations.
/// This allows EF Core tools to create a DbContext instance during design time.
/// </summary>
[ExcludeFromCodeCoverage]
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CnabDbContext>
{
    public CnabDbContext CreateDbContext(string[] args)
    {
        // Build configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Get connection string
        var connectionString = configuration.GetConnectionString("PostgresConnection")
            ?? "Host=localhost;Port=5432;Database=cnab_db;Username=postgres;Password=postgres";

        // Create options builder
        var optionsBuilder = new DbContextOptionsBuilder<CnabDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
            npgsqlOptions.MigrationsAssembly("CnabApi"));

        return new CnabDbContext(optionsBuilder.Options);
    }
}

