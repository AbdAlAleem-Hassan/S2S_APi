using Microsoft.AspNetCore.Authentication.JwtBearer;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using S2S.Domain.Contracts;
using S2S.Domain.Entities.IdentityModule;
using S2S.Persistence.IdentityData.DataSeed;
using S2S.Persistence.IdentityData.DbContexts;
using S2S.Services;
using S2S.ServicesAbstraction;
using S2S.Shared.Validators;
using System.Data.Common;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using S2S.Shared.Mappings;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure port for Heroku or other cloud providers
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

// Add services to the container.
#region Serilog Logging Conf
builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(builder.Configuration));
#endregion

builder.Services.AddControllers();

// Add FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterDTOValidator>();

// Configure Forwarded Headers for Reverse Proxy (Docker/Heroku)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth-limit", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5;
        opt.QueueLimit = 0;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

// Resolve Connection String dynamically for Cloud/Docker
var connectionString = ResolveSqlServerConnectionString(builder.Configuration);
builder.Services.AddDbContext<S2SIdentityDbContext>(option =>
{
    option.UseSqlServer(connectionString);
});

builder.Services.AddKeyedScoped<IDataInitializer, IdentityDataInitializer>("Identity");
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHttpClient<IAiTranslationService, AiTranslationService>();

// AutoMapper Configuration
builder.Services.AddAutoMapper(typeof(MappingProfiles));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 3;
    options.Lockout.AllowedForNewUsers = true;
    options.User.RequireUniqueEmail = true;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<S2SIdentityDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token here. Example: eyJhbGciOiJIUzI1..."
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddAuthentication(configureOptions =>
{
    configureOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    configureOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidIssuer = builder.Configuration["JWTOptions:Issuer"],
        ValidAudience = builder.Configuration["JWTOptions:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWTOptions:SecretKey"]!)),
    };
});

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

builder.Services.AddVersionedApiExplorer(setup =>
{
    setup.GroupNameFormat = "'v'VVV";
    setup.SubstituteApiVersionInUrl = true;
});





var app = builder.Build();

// Automatic Database Migrations on Startup
var applyMigrationsOnStartup = builder.Configuration.GetValue("ApplyMigrationsOnStartup", true);
if (applyMigrationsOnStartup)
{
    using var scope = app.Services.CreateScope();
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<S2SIdentityDbContext>();
        dbContext.Database.Migrate();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database migration failed on startup.");
    }
}

#region Data Seeding
//await app.SeedIdentityDatabase();
#endregion

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue("EnableSwagger", true))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// --- HELPER METHODS FOR CLOUD DEPLOYMENT ---

static string ResolveSqlServerConnectionString(IConfiguration configuration)
{
    var rawConnectionString = Environment.GetEnvironmentVariable("MSSQL_URL")
        ?? Environment.GetEnvironmentVariable("MSSQL")
        ?? Environment.GetEnvironmentVariable("DATABASE_URL")
        ?? configuration.GetConnectionString("DefaultConnection")
        ?? string.Empty;

    return BuildSqlServerConnectionString(rawConnectionString);
}

static string BuildSqlServerConnectionString(string rawConnectionString)
{
    if (string.IsNullOrWhiteSpace(rawConnectionString))
    {
        return rawConnectionString;
    }

    if (rawConnectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase)
        || rawConnectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
    {
        return rawConnectionString;
    }

    var normalized = rawConnectionString.Trim();

    if (normalized.StartsWith("jdbc:", StringComparison.OrdinalIgnoreCase))
    {
        normalized = normalized["jdbc:".Length..];
    }

    if (!normalized.StartsWith("sqlserver://", StringComparison.OrdinalIgnoreCase)
        && !normalized.StartsWith("mssql://", StringComparison.OrdinalIgnoreCase))
    {
        return rawConnectionString;
    }

    normalized = normalized.Replace("mssql://", "sqlserver://", StringComparison.OrdinalIgnoreCase);

    var withoutScheme = normalized["sqlserver://".Length..];
    var delimiterIndex = withoutScheme.IndexOfAny(new[] { ';', '/' });
    string hostPart;
    string? remainder = null;

    if (delimiterIndex >= 0)
    {
        hostPart = withoutScheme[..delimiterIndex];
        remainder = withoutScheme[delimiterIndex..];
    }
    else
    {
        hostPart = withoutScheme;
    }

    string? userName = null;
    string? password = null;

    if (hostPart.Contains('@', StringComparison.Ordinal))
    {
        var hostParts = hostPart.Split('@', 2);
        var credentialPart = hostParts[0];
        hostPart = hostParts[1];

        var credentialParts = credentialPart.Split(':', 2);
        userName = Uri.UnescapeDataString(credentialParts[0]);
        if (credentialParts.Length > 1)
        {
            password = Uri.UnescapeDataString(credentialParts[1]);
        }
    }

    var connectionBuilder = new DbConnectionStringBuilder();
    var dataSource = NormalizeServer(hostPart);
    if (!string.IsNullOrWhiteSpace(dataSource))
    {
        connectionBuilder["Server"] = dataSource;
    }

    if (!string.IsNullOrWhiteSpace(userName))
    {
        connectionBuilder["User Id"] = userName;
    }

    if (!string.IsNullOrWhiteSpace(password))
    {
        connectionBuilder["Password"] = password;
    }

    if (!string.IsNullOrWhiteSpace(remainder))
    {
        if (remainder.StartsWith("/", StringComparison.Ordinal))
        {
            var endIndex = remainder.IndexOf(';');
            var databasePart = endIndex > -1 ? remainder[1..endIndex] : remainder[1..];
            if (!string.IsNullOrWhiteSpace(databasePart))
            {
                connectionBuilder["Database"] = Uri.UnescapeDataString(databasePart);
            }

            remainder = endIndex > -1 ? remainder[(endIndex + 1)..] : string.Empty;
        }

        if (remainder.StartsWith(';'))
        {
            remainder = remainder[1..];
        }

        foreach (var segment in remainder.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segment.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            switch (key.ToLowerInvariant())
            {
                case "databasename":
                case "database":
                    connectionBuilder["Database"] = value;
                    break;
                case "user":
                case "userid":
                case "user id":
                case "username":
                    connectionBuilder["User Id"] = value;
                    break;
                case "password":
                    connectionBuilder["Password"] = value;
                    break;
                case "encrypt":
                    connectionBuilder["Encrypt"] = value;
                    break;
                case "trustservercertificate":
                    connectionBuilder["TrustServerCertificate"] = value;
                    break;
                case "logintimeout":
                case "login timeout":
                case "connect timeout":
                case "connection timeout":
                    connectionBuilder["Connect Timeout"] = value;
                    break;
                default:
                    connectionBuilder[key] = value;
                    break;
            }
        }
    }

    return connectionBuilder.ConnectionString;
}

static string NormalizeServer(string hostPart)
{
    if (string.IsNullOrWhiteSpace(hostPart))
    {
        return hostPart;
    }

    if (hostPart.StartsWith("[", StringComparison.Ordinal))
    {
        var endIndex = hostPart.IndexOf(']');
        if (endIndex > -1 && endIndex + 1 < hostPart.Length && hostPart[endIndex + 1] == ':')
        {
            var portPart = hostPart[(endIndex + 2)..];
            return $"{hostPart[..(endIndex + 1)]},{portPart}";
        }

        return hostPart;
    }

    var lastColonIndex = hostPart.LastIndexOf(':');
    if (lastColonIndex > 0 && hostPart.IndexOf(':') == lastColonIndex)
    {
        return $"{hostPart[..lastColonIndex]},{hostPart[(lastColonIndex + 1)..]}";
    }

    return hostPart;
}
