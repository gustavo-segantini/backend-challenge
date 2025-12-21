using HealthChecks.NpgSql;
using HealthChecks.Redis;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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
    /// Maps health check endpoints with different predicates for readiness and liveness probes.
    /// </summary>
    public static void MapHealthChecksEndpoints(this WebApplication app)
    {
        // Overall health check - all checks with details
        app.MapHealthChecks("/api/v1/health", new()
        {
            ResponseWriter = HealthCheckResponseWriter.WriteDetailedJsonResponse,
            AllowCachingResponses = false
        });

        // Readiness probe - database and cache checks
        app.MapHealthChecks("/api/v1/health/ready", new()
        {
            Predicate = check => check.Tags.Contains("database") || check.Tags.Contains("cache"),
            ResponseWriter = HealthCheckResponseWriter.WriteDetailedJsonResponse,
            AllowCachingResponses = false
        });

        // Liveness probe - API only
        app.MapHealthChecks("/api/v1/health/live", new()
        {
            Predicate = check => check.Tags.Contains("api"),
            ResponseWriter = HealthCheckResponseWriter.WriteDetailedJsonResponse,
            AllowCachingResponses = false
        });
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

/// <summary>
/// Helper class for writing health check responses.
/// </summary>
internal static class HealthCheckResponseWriter
{
    /// <summary>
    /// Writes a detailed JSON response for health checks with status and dependency information.
    /// </summary>
    public static async Task WriteDetailedJsonResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds,
                exception = e.Value.Exception?.Message
            })
        };

        await context.Response.WriteAsJsonAsync(response);
    }
}
