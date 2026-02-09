using System.Text;
using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Schedules;
using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Configure Marten for document storage with multi-tenancy support
builder.Services.AddMarten(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("PostgreSQL");
    options.Connection(connectionString ?? "Host=localhost;Database=bookingschedule;Username=postgres;Password=postgres");

    // Configure Tenant document with TenantId as identity
    options.Schema.For<Tenant>().Identity(t => t.Id);

    // Configure User document with UserId as identity
    options.Schema.For<User>().Identity(u => u.Id);

    // Configure CreationCode document with CreationCodeId as identity
    options.Schema.For<CreationCode>().Identity(c => c.Id);

    // Configure Schedule document with ScheduleId as identity
    options.Schema.For<Schedule>().Identity(s => s.Id);

    // Configure Booking document with BookingId as identity
    options.Schema.For<Booking>().Identity(b => b.Id);

    // Configure SubscriptionPlan document with SubscriptionPlanId as identity
    options.Schema.For<SubscriptionPlan>().Identity(p => p.Id);

    // Configure TenantSubscription document with TenantSubscriptionId as identity
    options.Schema.For<TenantSubscription>().Identity(s => s.Id);
});

// Register multi-tenancy services
builder.Services.AddScoped<ITenantContext, TenantContext>();

// Register authentication services
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<OtpService>();
builder.Services.AddSingleton<OtpNotificationService>();

// Configure JWT authentication
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("JWT SecretKey must be configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
        };
    });

// Configure authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("GlobalAdmin", policy =>
        policy.RequireRole("GlobalAdmin"));

    options.AddPolicy("TenantUser", policy =>
        policy.RequireClaim("TenantId"));
});

// Add FastEndpoints
builder.Services.AddFastEndpoints();

// Configure CORS for Blazor UI
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorUI", policy =>
    {
        policy.WithOrigins(
                builder.Configuration["Cors:AllowedOrigins"] ?? "https://localhost:5002")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

app.UseCors("AllowBlazorUI");

// Tenant resolution from request header
app.UseTenantResolution();

// Enable authentication and authorization
app.UseAuthentication();

// Trial validation middleware (must be after authentication)
app.UseTrialValidation();

app.UseAuthorization();

// Use FastEndpoints
app.UseFastEndpoints();

// Request logging
app.UseSerilogRequestLogging();

app.Run();
