using System.Diagnostics.CodeAnalysis;
using Serilog;
using CnabApi.Extensions;
using CnabApi.Middleware;

// Initial bootstrap logger (before configuration is loaded)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    
    // Configure Serilog from appsettings.json
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.With<CorrelationIdEnricher>());

    // ========== Configure Services ==========
    
    // Core services
    builder.Services
        .AddCoreServices()
        .AddApiVersioningConfiguration()
        .AddFluentValidationConfiguration()
        .AddSwaggerConfiguration()
        .AddCorsConfiguration()
        .AddProblemDetailsConfiguration()
        .AddOptionsConfiguration(builder.Configuration)
        .AddHttpClientsConfiguration()
        .AddDatabaseConfiguration(builder)
        .AddApplicationInsightsConfiguration(builder.Configuration)
        .AddCachingConfiguration(builder)
        .AddCompressionConfiguration()
        .AddApplicationServices()
        .AddMinioConfiguration(builder.Configuration)
        .AddHealthChecksConfiguration(builder);

    // Get JWT options for authentication configuration
    var jwtOptions = builder.Configuration.GetSection("Jwt").Get<CnabApi.Options.JwtOptions>() 
        ?? new CnabApi.Options.JwtOptions();
    if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
    {
        jwtOptions.SigningKey = builder.Configuration["JWT_SIGNING_KEY"]
            ?? "dev-signing-key-change-me-32-characters-minimum!!";
    }

    // Authentication
    builder.Services.AddJwtAuthenticationConfiguration(jwtOptions);

    // ========== Configure HTTP Request Pipeline ==========
    
    var app = builder.Build();

    app.UseEnableRequestBodyBuffering();
    app.UseCorrelationIdMiddleware();
    app.UseExceptionHandlingMiddleware();
    app.UseResponseCompression();
    app.UseSwaggerConfiguration();
    app.UseStaticFiles();
    app.UseHttpsRedirection();
    app.UseCors("ReactPolicy");
    app.UseAuthenticationConfiguration();
    app.MapHealthChecksEndpoints();
    app.UsePrometheusMetrics();
    app.MapControllers();

    // Run database migrations and seeding
    await app.RunDatabaseMigrationAndSeedingAsync();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Partial class to exclude Program from code coverage.
/// </summary>
[ExcludeFromCodeCoverage]
public partial class Program { };