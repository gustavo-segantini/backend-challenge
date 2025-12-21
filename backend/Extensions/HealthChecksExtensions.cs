using HealthChecks.NpgSql;
using HealthChecks.Redis;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Prometheus;

namespace CnabApi.Extensions;

/// <summary>
/// Extension methods for configuring health checks and metrics monitoring.
/// </summary>
public static class HealthChecksExtensions
{
    /// <summary>
    /// Adds health checks configuration with PostgreSQL, Redis, and custom checks.
    /// </summary>
    public static IServiceCollection AddHealthChecksConfiguration(this IServiceCollection services, WebApplicationBuilder builder)
    {
        var postgresConnectionString = builder.Configuration.GetConnectionString("PostgresConnection")
            ?? "Host=postgres;Port=5432;Database=cnab_db;Username=postgres;Password=postgres";

        var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection")
            ?? "localhost:6379";

        services
            .AddHealthChecks()
            .AddNpgSql(
                postgresConnectionString,
                name: "PostgreSQL",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["database", "postgres"])
            .AddRedis(
                redisConnectionString,
                name: "Redis",
                failureStatus: HealthStatus.Degraded,
                tags: ["cache", "redis"])
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
