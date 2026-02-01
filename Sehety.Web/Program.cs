using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<S2SIdentityDbContext>(option =>
{
	option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddKeyedScoped<IDataInitializer, IdentityDataInitializer>("Identity");
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddHttpClient<IAiTranslationService, AiTranslationService>();



builder.Services.AddIdentityCore<ApplicationUser>()
    .AddRoles<IdentityRole>()
	.AddEntityFrameworkStores<S2SIdentityDbContext>();

builder.Services.AddSwaggerGen();
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

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterDTOValidator>();

var app = builder.Build();

#region Data Seeding

//await app.SeedIdentityDatabase();
#endregion

// Configure the HTTP request pipeline.

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwagger();
}
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
