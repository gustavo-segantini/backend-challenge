/// <summary>
/// Configuration for optional Application Insights telemetry.
/// 
/// To enable Application Insights:
/// 
/// 1. Install the NuGet package (already installed):
///    dotnet add package Microsoft.ApplicationInsights.AspNetCore
/// 
/// 2. Add to Program.cs after creating builder:
///    builder.Services.AddApplicationInsightsTelemetry();
/// 
/// 3. Set the instrumentation key in appsettings.json:
///    "ApplicationInsights": {
///      "InstrumentationKey": "YOUR-INSTRUMENTATION-KEY"
///    }
/// 
/// OR set via environment variable:
///    APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=YOUR-KEY
/// 
/// 4. The middleware will automatically track:
///    - Request/Response timing
///    - Exceptions and errors
///    - Dependencies (Database, HTTP calls)
///    - Performance counters
/// 
/// To view telemetry:
/// - Log in to Azure Portal
/// - Navigate to your Application Insights resource
/// - Check Live Metrics, Performance, Failures, etc.
/// 
/// NOTE: This is optional. The application works fine without it.
/// Serilog is the primary logging solution in this project.
/// </summary>
namespace CnabApi.Configuration;

public class ApplicationInsightsConfiguration
{
    // Example configuration - uncomment in Program.cs if needed
    /*
    // In Program.cs, after creating the builder:
    
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.InstrumentationKey = builder.Configuration["ApplicationInsights:InstrumentationKey"];
        options.EnableActiveTelemetryConfigurationSetup = true;
    });

    // This will automatically instrument:
    // - HTTP requests and responses
    // - Database queries
    // - Exceptions and traces
    // - Performance metrics
    */
}
