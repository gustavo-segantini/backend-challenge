using System.Diagnostics.CodeAnalysis;
using Serilog;
using CnabApi.Extensions;
using Hellang.Middleware.ProblemDetails;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "CnabApi")
    .Enrich.WithMachineName()
    .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [CorrelationId: {CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/cnab-api-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [CorrelationId: {CorrelationId}] {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

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

    app.UseCorrelationIdMiddleware();
    app.UseExceptionHandlingMiddleware();
    app.UseResponseCompression();
    app.UseSwaggerConfiguration();
    app.UseStaticFiles();
    app.UseHttpsRedirection();
    app.UseCors("ReactPolicy");
    app.UseAuthenticationConfiguration();
    app.UseHealthChecks("/api/v1/health");
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