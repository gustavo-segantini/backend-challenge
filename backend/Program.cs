using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using CnabApi.Data;
using CnabApi.Services;
using CnabApi.Middleware;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ContentRootPath = AppContext.BaseDirectory,
    WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
});

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "CNAB Transaction API",
        Description = "API for managing CNAB file uploads and transaction queries",
        Contact = new OpenApiContact
        {
            Name = "Backend Challenge",
            Url = new Uri("https://github.com/your-repo/backend-challenge")
        }
    });

    // Include XML comments
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    options.IncludeXmlComments(xmlPath);
});

// Add CORS for React frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactPolicy", policyBuilder =>
    {
        policyBuilder
            .WithOrigins("http://localhost:3000", "http://localhost:5173")
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Add DbContext - Use InMemory for Test environment, PostgreSQL for others
if (builder.Environment.EnvironmentName == "Test")
{
    builder.Services.AddDbContext<CnabDbContext>(options =>
        options.UseInMemoryDatabase("TestDatabase"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("PostgresConnection") 
        ?? "Host=postgres;Port=5432;Database=cnab_db;Username=postgres;Password=postgres";

    builder.Services.AddDbContext<CnabDbContext>(options =>
        options.UseNpgsql(connectionString, npgsqlOptions =>
            npgsqlOptions.MigrationsAssembly("CnabApi")));
}

// Add services
builder.Services.AddScoped<ICnabParserService, CnabParserService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<ICnabUploadService, CnabUploadService>();

var app = builder.Build();

// Add global exception handling middleware (first in pipeline)
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "CNAB API v1");
    options.RoutePrefix = "swagger";
});
app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseCors("ReactPolicy");

// Map controllers - must be before app.Run()
app.MapControllers();

// Run migrations on startup (only if database is available and not in Test environment)
if (app.Environment.EnvironmentName != "Test")
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CnabDbContext>();
        // Suppress the pending model changes warning for migrations
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Migrations");

        logger.LogWarning("Checking for pending migrations...");
        await db.Database.MigrateAsync();
        logger.LogInformation("Migrations completed successfully");
    }
    catch (Exception ex)
    {
        // Don't fail startup if migrations error - allow app to run
        Console.WriteLine($"Migration note: {ex.Message}");
    }
}

app.Run();

/// <summary>
/// Partial class to exclude Program from code coverage.
/// </summary>
[ExcludeFromCodeCoverage]
public partial class Program { };
