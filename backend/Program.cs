using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using CnabApi.Data;
using CnabApi.Data.Seed;
using CnabApi.Services;
using CnabApi.Services.Auth;
using CnabApi.Middleware;
using CnabApi.Options;
using CnabApi.Models;

var builder = WebApplication.CreateBuilder(args);

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

app.UseAuthentication();
app.UseAuthorization();

// Map controllers - must be before app.Run()
app.MapControllers();

// Run migrations on startup (only if database is available and not in Test environment)
try
{
    await DataSeeder.SeedAsync(app.Services);
}
catch (Exception ex)
{
    Console.WriteLine($"Migration/Seed note: {ex.Message}");
}

app.Run();

/// <summary>
/// Partial class to exclude Program from code coverage.
/// </summary>
[ExcludeFromCodeCoverage]
public partial class Program { };
