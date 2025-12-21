using System.Diagnostics.CodeAnalysis;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;

namespace CnabApi.Extensions;

/// <summary>
/// Extension methods for configuring Application Insights telemetry.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ApplicationInsightsExtensions
{
    /// <summary>
    /// Adds Application Insights telemetry to the service collection.
    /// Automatically instruments HTTP requests, exceptions, dependencies, and performance metrics.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApplicationInsightsTelemetryConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Get the connection string from configuration (preferred over InstrumentationKey)
        var connectionString = configuration["ApplicationInsights:ConnectionString"];
        
        // Fallback to InstrumentationKey if ConnectionString is not provided
        if (string.IsNullOrEmpty(connectionString))
        {
            var instrumentationKey = configuration["ApplicationInsights:InstrumentationKey"];
            if (!string.IsNullOrEmpty(instrumentationKey))
            {
                connectionString = $"InstrumentationKey={instrumentationKey}";
            }
        }

        // Only add Application Insights if connection string or instrumentation key is configured
        if (!string.IsNullOrEmpty(connectionString))
        {
            var options = new ApplicationInsightsServiceOptions
            {
                ConnectionString = connectionString,
                EnableActiveTelemetryConfigurationSetup = true,
                RequestCollectionOptions =
                {
                    TrackExceptions = true,
                    InjectResponseHeaders = true
                }
            };

            services.AddApplicationInsightsTelemetry(options);

            return services;
        }

        // If no connection string or instrumentation key is configured, Application Insights is disabled
        // This allows the application to run without it
        return services;
    }
}
