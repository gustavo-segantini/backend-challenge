using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using FluentValidation;
using System.Reflection;
using System.Text;
using System.Net.Http.Headers;
using System.Diagnostics.CodeAnalysis;
using CnabApi.Data;
using CnabApi.Options;
using CnabApi.Models;
using CnabApi.Validators;

namespace CnabApi.Extensions;

/// <summary>
/// Extension methods for configuring services in the dependency injection container.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core API services (controllers, endpoints explorer).
    /// </summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        return services;
    }

    /// <summary>
    /// Adds API versioning configuration.
    /// </summary>
    public static IServiceCollection AddApiVersioningConfiguration(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.ReportApiVersions = true;
        });

        return services;
    }

    /// <summary>
    /// Adds FluentValidation with automatic registration from assembly.
    /// </summary>
    public static IServiceCollection AddFluentValidationConfiguration(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<TransactionValidator>();
        return services;
    }

    /// <summary>
    /// Adds Swagger/OpenAPI documentation configuration with enhanced documentation.
    /// </summary>
    public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1",
                Title = "CNAB Transaction API",
                Description = """
                    Comprehensive API for managing CNAB file uploads and transaction queries.
                    
                    **Features:**
                    - CNAB file upload and parsing with real-time validation
                    - Transaction queries with CPF-based filtering and pagination
                    - JWT-based authentication with GitHub OAuth integration
                    - Balance calculation and transaction categorization
                    - Health checks and Prometheus metrics
                    
                    **Authentication:**
                    All transaction endpoints require Bearer token authentication via JWT.
                    Use /api/v1/auth/login or /api/v1/auth/github/login to obtain tokens.
                    """,
                Contact = new OpenApiContact
                {
                    Name = "Backend Challenge",
                    Url = new Uri("https://github.com/gustavo-segantini/backend-challenge")
                },
                License = new OpenApiLicense
                {
                    Name = "MIT"
                }
            });

            // Include XML comments from assembly
            var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            // Add tags for grouping endpoints
            options.TagActionsBy(api =>
            {
                if (api.GroupName != null)
                    return new[] { api.GroupName };

                var controllerActionDescriptor = api.ActionDescriptor as Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor;
                return new[] { controllerActionDescriptor?.ControllerName ?? "Default" };
            });

            // Enable JWT bearer authentication in Swagger UI
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = """
                    JWT Bearer token authentication.
                    
                    Example header:
                    Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJjcmVhYnRlZCIsImlhdCI6MTcwMzE0MzYwMH0.sY6hVLd...
                    
                    Obtain token via:
                    - POST /api/v1/auth/login (credentials)
                    - GET /api/v1/auth/github/login (GitHub OAuth)
                    """
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

        return services;
    }

    /// <summary>
    /// Adds CORS configuration for React frontend.
    /// </summary>
    public static IServiceCollection AddCorsConfiguration(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("ReactPolicy", policyBuilder =>
            {
                policyBuilder
                    .WithOrigins("http://localhost:3000", "http://localhost:5173")
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        return services;
    }

    /// <summary>
    /// Adds Problem Details middleware configuration for RFC 7807 standardized error responses.
    /// </summary>
    public static IServiceCollection AddProblemDetailsConfiguration(this IServiceCollection services)
    {
        services.AddProblemDetails();
        return services;
    }

    /// <summary>
    /// Adds options bindings from configuration.
    /// </summary>
    public static IServiceCollection AddOptionsConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<GitHubOAuthOptions>(configuration.GetSection("GitHubOAuth"));

        var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
        {
            jwtOptions.SigningKey = configuration["JWT_SIGNING_KEY"]
                ?? "dev-signing-key-change-me-32-characters-minimum!!";
        }
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(jwtOptions));

        return services;
    }

    /// <summary>
    /// Adds HTTP client configuration for GitHub OAuth.
    /// </summary>
    public static IServiceCollection AddHttpClientsConfiguration(this IServiceCollection services)
    {
        services.AddHttpClient("GitHubOAuth", client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("cnab-api-auth");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        return services;
    }

    /// <summary>
    /// Adds database context configuration (InMemory for Test, PostgreSQL for others).
    /// </summary>
    public static IServiceCollection AddDatabaseConfiguration(this IServiceCollection services, WebApplicationBuilder builder)
    {
        if (builder.Environment.EnvironmentName == "Test")
        {
            services.AddDbContext<CnabDbContext>(options =>
                options.UseInMemoryDatabase("TestDatabase"));
        }
        else
        {
            var connectionString = builder.Configuration.GetConnectionString("PostgresConnection")
                ?? "Host=postgres;Port=5432;Database=cnab_db;Username=postgres;Password=postgres";

            services.AddDbContext<CnabDbContext>(options =>
                options.UseNpgsql(connectionString, npgsqlOptions =>
                    npgsqlOptions.MigrationsAssembly("CnabApi")));
        }

        return services;
    }

    /// <summary>
    /// Adds caching configuration (Redis for production, in-memory for test).
    /// </summary>
    public static IServiceCollection AddCachingConfiguration(this IServiceCollection services, WebApplicationBuilder builder)
    {
        if (builder.Environment.EnvironmentName == "Test")
        {
            services.AddMemoryCache();
        }
        else
        {
            var redisConnection = builder.Configuration.GetConnectionString("RedisConnection")
                ?? "localhost:6379";

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
            });
        }

        return services;
    }

    /// <summary>
    /// Adds response compression configuration.
    /// </summary>
    public static IServiceCollection AddCompressionConfiguration(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
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

        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = System.IO.Compression.CompressionLevel.Optimal;
        });

        return services;
    }

    /// <summary>
    /// Adds JWT authentication configuration.
    /// </summary>
    public static IServiceCollection AddJwtAuthenticationConfiguration(this IServiceCollection services, JwtOptions jwtOptions)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

        services.AddAuthentication(options =>
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

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Registers all application services using Scrutor for automatic registration.
    /// Automatically discovers and registers implementations by their interfaces.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Use Scrutor to automatically register all services
        services.Scan(scan => scan
            .FromAssemblyOf<Program>()
            .AddClasses(filter => filter
                .Where(t => (t.Name.EndsWith("Service") || t.Name.EndsWith("Handler")) 
                       && !t.IsAbstract 
                       && !t.IsInterface))
            .AsMatchingInterface()
            .WithScopedLifetime());

        // Register password hasher as singleton
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        return services;
    }

    /// <summary>
    /// Adds Application Insights telemetry configuration.
    /// </summary>
    public static IServiceCollection AddApplicationInsightsConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddApplicationInsightsTelemetryConfiguration(configuration);
    }
}
