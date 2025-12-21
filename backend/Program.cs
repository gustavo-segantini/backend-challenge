using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Core;
using FluentValidation;
using Hellang.Middleware.ProblemDetails;
using CnabApi.Data;
using CnabApi.Data.Seed;
using CnabApi.Services;
using CnabApi.Services.Auth;
using CnabApi.Middleware;
using CnabApi.Options;
using CnabApi.Models;
using CnabApi.Validators;

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

    // Add services

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
});

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<TransactionValidator>();
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

    // Enable JWT bearer authentication in Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
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

// Add Problem Details (RFC 7807) for standardized error responses
builder.Services.AddProblemDetails(options =>
{
    options.MapToStatusCode<NotImplementedException>(StatusCodes.Status501NotImplemented);
    options.MapToStatusCode<HttpRequestException>(StatusCodes.Status503ServiceUnavailable);
    options.MapToStatusCode<Exception>(StatusCodes.Status500InternalServerError);
});

// Options bindings
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<GitHubOAuthOptions>(builder.Configuration.GetSection("GitHubOAuth"));

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
{
    // Fallback for local/dev; should be overridden via environment variable JWT_SIGNING_KEY
    jwtOptions.SigningKey = builder.Configuration["JWT_SIGNING_KEY"]
        ?? "dev-signing-key-change-me-32-characters-minimum!!";
}
builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(jwtOptions));

builder.Services.AddHttpClient("GitHubOAuth", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("cnab-api-auth");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
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
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Add caching - Redis for production, in-memory for test
if (builder.Environment.EnvironmentName == "Test")
{
    builder.Services.AddMemoryCache();
}
else
{
    var redisConnection = builder.Configuration.GetConnectionString("RedisConnection") 
        ?? "localhost:6379";
    
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
    });
}

// Add response compression
builder.Services.AddResponseCompression(options =>
{
    options.Providers.Add<GzipCompressionProvider>();
    options.EnableForHttps = true;
    options.MimeTypes = new[]
    {
        "application/json",
        "application/xml",
        "text/plain",
        "text/html",
        "text/css",
        "text/javascript",
        "application/javascript"
    };
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Optimal;
});

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = signingKey,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Add Correlation ID middleware (must be first)
app.UseMiddleware<CorrelationIdMiddleware>();

// Add Problem Details middleware
app.UseProblemDetails();

// Add global exception handling middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Enable compression
app.UseResponseCompression();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "CNAB API v1");
    options.RoutePrefix = "swagger";
});
app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseCors("ReactPolicy");

app.UseAuthentication();
app.UseAuthorization();

// Map controllers - must be before app.Run()
app.MapControllers();

// Run migrations on startup (only if database is available and not in Test environment)
try
{
    await DataSeeder.SeedAsync(app.Services);
    Log.Information("Database migrations and seeding completed successfully");
}
catch (Exception ex)
{
    Log.Warning($"Migration/Seed note: {ex.Message}");
}

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