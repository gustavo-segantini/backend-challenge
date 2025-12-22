using CnabApi.Data.Seed;
using CnabApi.Middleware;
using Serilog;
using System.Diagnostics.CodeAnalysis;

namespace CnabApi.Extensions;

/// <summary>
/// Extension methods for configuring the HTTP request pipeline middleware.
/// </summary>
[ExcludeFromCodeCoverage]
public static class MiddlewareExtensions
{
    /// <summary>
    /// Adds request body buffering middleware (must be first in the pipeline).
    /// </summary>
    public static WebApplication UseEnableRequestBodyBuffering(this WebApplication app)
    {
        app.UseMiddleware<EnableRequestBodyBufferingMiddleware>();
        return app;
    }

    /// <summary>
    /// Adds correlation ID middleware (must be second in the pipeline).
    /// </summary>
    public static WebApplication UseCorrelationIdMiddleware(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        return app;
    }

    /// <summary>
    /// Adds global exception handling middleware.
    /// </summary>
    public static WebApplication UseExceptionHandlingMiddleware(this WebApplication app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        return app;
    }

    /// <summary>
    /// Configures Swagger UI and API documentation endpoints.
    /// </summary>
    public static WebApplication UseSwaggerConfiguration(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "CNAB API v1");
            options.RoutePrefix = "swagger";
        });

        return app;
    }

    /// <summary>
    /// Configures authentication and authorization middleware.
    /// </summary>
    public static WebApplication UseAuthenticationConfiguration(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    /// <summary>
    /// Runs database migrations and seeding on startup.
    /// </summary>
    public static async Task RunDatabaseMigrationAndSeedingAsync(this WebApplication app)
    {
        try
        {
            await DataSeeder.SeedAsync(app.Services);
            Log.Information("Database migrations and seeding completed successfully");
        }
        catch (Exception ex)
        {
            Log.Warning($"Migration/Seed note: {ex.Message}");
        }
    }
}
