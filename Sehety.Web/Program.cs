using FirebaseAdmin;
using FluentValidation;
using FluentValidation.AspNetCore;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using S2S.Domain.Contracts;
using S2S.Domain.Entities.IdentityModule;
using S2S.Persistence.IdentityData.DataSeed;
using S2S.Persistence.IdentityData.DbContexts;
using S2S.Services;
using S2S.ServicesAbstraction;
using S2S.Shared.Constants;
using S2S.Shared.Mappings;
using S2S.Shared.Validators;
using S2S.Web.Extensions;
using S2S.Web.Health;
using S2S.Web.Middleware;
using S2S.Web.Services;
using Serilog;
using System.Data.Common;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configure port for Heroku or other cloud providers
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

var googleCredentialsPath = builder.Configuration["Google:ApplicationCredentials"];
if (!string.IsNullOrWhiteSpace(googleCredentialsPath)
    && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")))
{
    Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", googleCredentialsPath);
}

var hasGroqApiKey = !string.IsNullOrWhiteSpace(builder.Configuration["Groq:ApiKey"])
    || !string.IsNullOrWhiteSpace(builder.Configuration["GROQ_API_KEY"]);
var googleCredentialsSection = builder.Configuration.GetSection("Google:Credentials");
var hasGoogleCredentials = !string.IsNullOrWhiteSpace(builder.Configuration["Google:CredentialsJson"])
    || !string.IsNullOrWhiteSpace(builder.Configuration["Google:ApplicationCredentials"])
    || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS"))
    || googleCredentialsSection.GetChildren().Any(child => !string.IsNullOrWhiteSpace(child.Value));

// Add services to the container.
#region Serilog Logging Conf
builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(builder.Configuration));
#endregion

builder.Services.AddControllers().AddJsonOptions(options =>
{
	// السطر ده هيخلي أي حقل قيمته Null ميظهرش في الـ JSON نهائياً
	options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Add FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterDTOValidator>();

// Add Anti-forgery for CSRF protection (web cookie-based flow)
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = CookieNames.XsrfToken;
    options.Cookie.HttpOnly = false;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Configure Forwarded Headers for Reverse Proxy (Docker/Heroku)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsDefaults.AllowFrontendPolicy,
        policy =>
        {
            policy.WithOrigins(
                    "https://s2sai.online",
                    "https://www.s2sai.online",
                    "http://localhost:3000",
                    "http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter(RateLimitPolicies.AuthLimit, opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5;
        opt.QueueLimit = 0;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.AddPolicy(RateLimitPolicies.OtpRequestLimit, context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var email = context.Request.Query["email"].ToString();
        var key = string.IsNullOrWhiteSpace(email)
            ? ip
            : $"{ip}:{email.Trim().ToLowerInvariant()}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });

    options.AddPolicy(RateLimitPolicies.OtpVerifyLimit, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    // Dedicated stricter rate limit for ChangePassword: 3 attempts per 10 minutes per IP
    options.AddPolicy(RateLimitPolicies.ChangePasswordLimit, context =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString(),
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0
        }));

    // Rate limit for STT (audio-to-sign) requests: 10 per minute per IP
    options.AddPolicy(RateLimitPolicies.SttLimit, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    // Rate limit for media serving: 60 requests per minute per IP
    options.AddPolicy(RateLimitPolicies.MediaLimit, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = MediaDefaults.MediaRateLimitPermits,
                Window = TimeSpan.FromMinutes(MediaDefaults.MediaRateLimitWindowMinutes),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
});

// Resolve Connection String dynamically for Cloud/Docker
var connectionString = ResolveSqlServerConnectionString(builder.Configuration);
builder.Services.AddDbContext<S2SIdentityDbContext>(option =>
{
    option.UseSqlServer(connectionString);
});

builder.Services.AddKeyedScoped<IDataInitializer, IdentityDataInitializer>("Identity");
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHttpClient<ISpeechToTextService, GroqSpeechToTextService>(client =>
{
    var timeoutSeconds = builder.Configuration.GetValue("SttSettings:TimeoutSeconds", 30);
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});
builder.Services.AddSingleton<ITextToSpeechService, GoogleTextToSpeechService>();
builder.Services.AddHttpClient<IAiTranslationService, AiTranslationService>();
builder.Services.AddHostedService<MediaCleanupService>();
builder.Services.AddHostedService<UnverifiedAccountCleanupService>();

// Health checks with database connectivity verification
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["db"]);

// AutoMapper Configuration
//builder.Services.AddAutoMapper(typeof(MappingProfiles).Assembly);
builder.Services.AddAutoMapper(cfg => { }, typeof(MappingProfiles).Assembly);

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = AuthDefaults.PasswordMinLength;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(AuthDefaults.AccountLockoutMinutes);
    options.Lockout.MaxFailedAccessAttempts = AuthDefaults.MaxFailedAccessAttempts;
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

#region Fire Base Admin SDK Initialization
var firebaseJson = builder.Configuration["FIREBASE_CONFIG"];

if (!string.IsNullOrEmpty(firebaseJson))
{
	FirebaseApp.Create(new AppOptions()
	{
        Credential = CredentialFactory.FromJson(firebaseJson, "service_account")
	});
}
else
{
	var firebaseCredPath = builder.Configuration["Firebase:CredentialsPath"];
	var fullPath = Path.Combine(Directory.GetCurrentDirectory(), firebaseCredPath!);

	if (File.Exists(fullPath))
	{
		FirebaseApp.Create(new AppOptions()
		{
            Credential = CredentialFactory.FromFile(fullPath, "service_account")
		});
	}
	else
	{
		Console.WriteLine("Firebase warning: Credentials file not found!");
	}
}
#endregion





var app = builder.Build();

if (!hasGroqApiKey)
{
    app.Logger.LogWarning("Groq API key is missing. STT endpoints will fail.");
}

if (!hasGoogleCredentials)
{
    app.Logger.LogWarning("Google TTS credentials are missing. Audio responses will be skipped.");
}

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
await app.SeedIdentityDatabase();
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
app.UseStaticFiles();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors(CorsDefaults.AllowFrontendPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.ToString(),
                description = e.Value.Description
            })
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});

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
