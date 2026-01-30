using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using S2S.Domain.Contracts;
using S2S.Domain.Entities.IdentityModule;
using S2S.Persistence.DbContexts;
using S2S.Persistence.IdentityData.DataSeed;
using S2S.Persistence.IdentityData.DbContexts;
using S2S.Services;
using S2S.ServicesAbstraction;
using S2S.Web.Extensions;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using S2S.Shared.Mappings;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin() // In production, replace with specific origins
                  .AllowAnyHeader()
                  .AllowAnyMethod();
            // Note: If using HttpOnly cookies with a specific origin, use .WithOrigins(...) and .AllowCredentials()
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

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddDbContext<S2SIdentityDbContext>(option =>
{
	option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddKeyedScoped<IDataInitializer, IdentityDataInitializer>("Identity");
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// AutoMapper Configuration
builder.Services.AddAutoMapper(typeof(MappingProfiles));


builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
})
    .AddRoles<IdentityRole>()
	.AddEntityFrameworkStores<S2SIdentityDbContext>();

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

#region Data Seeding

//await app.SeedIdentityDatabase();
#endregion

// Configure the HTTP request pipeline.

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}
app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseRateLimiter(); // Rate limiter before auth to prevent brute force

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
