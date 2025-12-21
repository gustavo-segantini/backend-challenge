using HealthChecks.NpgSql;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Prometheus;

namespace CnabApi.Extensions;

/// <summary>
/// Extension methods for configuring health checks and metrics monitoring.
/// </summary>
public static class HealthChecksExtensions
{
    /// <summary>
    /// Adds health checks configuration with PostgreSQL and custom checks.
    /// </summary>
    public static IServiceCollection AddHealthChecksConfiguration(this IServiceCollection services, WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("PostgresConnection")
            ?? "Host=postgres;Port=5432;Database=cnab_db;Username=postgres;Password=postgres";

        services
            .AddHealthChecks()
            .AddNpgSql(
                connectionString,
                name: "PostgreSQL",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["database", "postgres"])
            .AddCheck(
                "API",
                () => HealthCheckResult.Healthy("API is running"),
                tags: ["api"]);

        return services;
    }

    /// <summary>
    /// Configures Prometheus metrics middleware.
    /// </summary>
    public static void UsePrometheusMetrics(this WebApplication app)
    {
        // Configure Prometheus metrics endpoint
        app.UseMetricServer();
    }
}
