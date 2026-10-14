using LibraryManagement.Api.Data;
using LibraryManagement.Api.Models;
using LibraryManagement.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Caching.Memory;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Caching services
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<BookCacheVersionService>();

builder.Services.AddControllers();

builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "DefaultConnection is missing.")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDevelopment", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IPasswordHasher<Admin>,
    PasswordHasher<Admin>>();
builder.Services.AddScoped<IPasswordHasher<LibraryUser>,
    PasswordHasher<LibraryUser>>();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer is missing.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Jwt:Audience is missing.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey)),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<FineCalculationService>();

var app = builder.Build();

await AdminSeeder.SeedAsync(
    app.Services,
    app.Configuration);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AngularDevelopment");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();