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
using CnabApi.Services;
using Minio;
using AspNetCoreRateLimit;

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
                    - Transaction queries grouped by store with pagination
                    - JWT-based authentication with GitHub OAuth integration
                    - Balance calculation and transaction categorization
                    - Health checks and Prometheus metrics
                    - Rate limiting for API protection
                    
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
    /// Adds rate limiting configuration to protect API from abuse.
    /// </summary>
    public static IServiceCollection AddRateLimitingConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // Load rate limit configuration from appsettings
        services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
        services.Configure<IpRateLimitPolicies>(configuration.GetSection("IpRateLimitPolicies"));

        // Add in-memory cache for rate limit counters
        services.AddMemoryCache();
        
        // Add rate limit services
        services.AddInMemoryRateLimiting();
        
        // Add rate limit configuration store
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

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
        services.Configure<UploadProcessingOptions>(configuration.GetSection(UploadProcessingOptions.SectionName));

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
    /// Also registers IConnectionMultiplexer for distributed locking and queue services.
    /// </summary>
    public static IServiceCollection AddCachingConfiguration(this IServiceCollection services, WebApplicationBuilder builder)
    {
        if (builder.Environment.EnvironmentName == "Test")
        {
            services.AddMemoryCache();
            // For test environment, register mock/stub services that work correctly for tests
            // These provide basic queue and lock functionality without Redis
            services.AddSingleton<Services.Interfaces.IUploadQueueService, Services.Testing.MockUploadQueueService>();
            services.AddSingleton<Services.Interfaces.IDistributedLockService, Services.Testing.MockDistributedLockService>();
        }
        else
        {
            var redisConnection = builder.Configuration.GetConnectionString("RedisConnection")
                ?? "localhost:6379";

            // Add IConnectionMultiplexer for distributed locking and queues
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
                StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnection));

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
            });

            // Register Redis-based queue and lock services as singletons
            // (required for use in HostedService which is singleton)
            services.AddSingleton<Services.Interfaces.IUploadQueueService, Services.RedisUploadQueueService>();
            services.AddSingleton<Services.Interfaces.IDistributedLockService, Services.RedisDistributedLockService>();
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
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, WebApplicationBuilder builder)
    {
        // Use Scrutor to automatically register all services
        services.Scan(scan => scan
            .FromAssemblyOf<Program>()
            .AddClasses(filter => filter
                .Where(t => (t.Name.EndsWith("Service") || t.Name.EndsWith("Handler")) 
                       && !t.IsAbstract 
                       && !t.IsInterface
                       && t.Name != "UploadProcessingHostedService" // Exclude HostedService as it's registered manually
                       && t.Namespace != "CnabApi.Services.Testing" // Exclude test mocks
                       && t.Namespace != "CnabApi.Services.UploadProcessing")) // Exclude upload processing strategies (registered explicitly)
            .AsMatchingInterface()
            .WithScopedLifetime());

        // Explicitly register HashService to ensure it's available (stateless, can be singleton)
        services.AddSingleton<Services.Interfaces.IHashService, Services.HashService>();

        // Register Unit of Work as scoped (one per request/operation)
        services.AddScoped<Services.UnitOfWork.IUnitOfWork, Services.UnitOfWork.EfCoreUnitOfWork>();

        // Register line processing services explicitly (not caught by Scrutor due to naming)
        services.AddScoped<Services.LineProcessing.ILineProcessor, Services.LineProcessing.LineProcessor>();
        services.AddScoped<Services.LineProcessing.ICheckpointManager, Services.LineProcessing.CheckpointManager>();

        // Register Status Code Strategy Factory as singleton
        services.AddSingleton<Services.StatusCodes.UploadStatusCodeStrategyFactory>();

        // Register password hasher as singleton
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        // Register upload processing strategy based on environment
        // This follows the Strategy pattern and Dependency Inversion Principle
        if (builder.Environment.EnvironmentName == "Test")
        {
            services.AddScoped<Services.Interfaces.IUploadProcessingStrategy, Services.UploadProcessing.SynchronousUploadProcessingStrategy>();
        }
        else
        {
            services.AddScoped<Services.Interfaces.IUploadProcessingStrategy, Services.UploadProcessing.AsynchronousUploadProcessingStrategy>();
        }

        return services;
    }

    /// <summary>
    /// Adds Application Insights telemetry configuration.
    /// </summary>
    public static IServiceCollection AddApplicationInsightsConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddApplicationInsightsTelemetryConfiguration(configuration);
    }

    /// <summary>
    /// Adds MinIO object storage configuration.
    /// 
    /// Configures:
    /// - MinIO client using official SDK pattern (WithEndpoint, WithCredentials, Build)
    /// - IObjectStorageService implementation (MinioStorageService)
    /// - MinioInitializationService as IHostedService for async bucket creation
    /// 
    /// Note: Bucket initialization happens asynchronously via IHostedService
    /// to avoid blocking I/O during dependency injection setup.
    /// </summary>
    public static IServiceCollection AddMinioConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // Register MinIO configuration from appsettings
        var minioConfig = configuration.GetSection("MinIO").Get<Services.ObjectStorage.MinioStorageConfiguration>()
            ?? new Services.ObjectStorage.MinioStorageConfiguration();

        services.AddSingleton(minioConfig);

        // Register MinIO client using official SDK pattern from readme
        services.AddMinio(configureClient => configureClient
            .WithEndpoint(minioConfig.Endpoint)
            .WithCredentials(minioConfig.AccessKey, minioConfig.SecretKey)
            .WithSSL(minioConfig.UseSSL)
            .Build());

        // Register object storage service
        services.AddScoped<IObjectStorageService, Services.ObjectStorage.MinioStorageService>();

        // Register MinIO initialization as hosted service for async bucket setup
        services.AddHostedService<Services.ObjectStorage.MinioInitializationService>();

        return services;
    }
}
