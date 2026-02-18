using System.Text;
using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleNotificationService;
using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.BackgroundJobs;
using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.Database;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Schedules;
using BookingScheduleSystem.Api.Infrastructure.Notifications;
using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Amazon.Runtime;
using BookingScheduleSystem.Api.Infrastructure.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Exporter;
using OpenTelemetry.Extensions.AWS.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog (reads from appsettings, including the OpenTelemetry sink)
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Configure OpenTelemetry
var otelServiceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "BookingScheduleSystem.Api";
var otelExporterEndpoint = builder.Configuration["OpenTelemetry:ExporterEndpoint"];
var isProduction = !builder.Environment.IsDevelopment();

// In production, propagate both W3C TraceContext and X-Ray trace headers (ALB sets X-Amzn-Trace-Id)
if (isProduction)
{
    Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([
        new TraceContextPropagator(),
        new BaggagePropagator(),
        new AWSXRayPropagator()
    ]));
}

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: otelServiceName,
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0")
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName
        }))
    .WithTracing(tracing =>
    {
        tracing
            .AddXRayTraceId()
            .AddAspNetCoreInstrumentation(opts =>
            {
                opts.RecordException = true;
            })
            .AddHttpClientInstrumentation()
            .AddSource("Npgsql");

        if (!string.IsNullOrEmpty(otelExporterEndpoint) && isProduction)
        {
            // Production: export to X-Ray via OTLP with SigV4 signing, 10% sampling for cost control
            tracing.SetSampler(new TraceIdRatioBasedSampler(0.1));
            tracing.AddOtlpExporter(opts =>
            {
                opts.Endpoint = new Uri(otelExporterEndpoint);
                opts.Protocol = OtlpExportProtocol.HttpProtobuf;
                opts.HttpClientFactory = () =>
                {
                    var credentials = FallbackCredentialsFactory.GetCredentials();
                    var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "ap-southeast-1";
                    return new HttpClient(new XRaySigV4Handler(credentials, region));
                };
            });
        }
        else if (!string.IsNullOrEmpty(otelExporterEndpoint))
        {
            // Dev with custom endpoint (e.g., local collector)
            tracing.AddOtlpExporter(opts => opts.Endpoint = new Uri(otelExporterEndpoint));
        }

        if (builder.Environment.IsDevelopment())
        {
            tracing.AddConsoleExporter();
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();

        // Don't export metrics to X-Ray (traces-only endpoint)
        if (!string.IsNullOrEmpty(otelExporterEndpoint) && !isProduction)
        {
            metrics.AddOtlpExporter(opts => opts.Endpoint = new Uri(otelExporterEndpoint));
        }

        if (builder.Environment.IsDevelopment())
        {
            metrics.AddConsoleExporter();
        }
    });

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

    // Configure InAppNotification document
    options.Schema.For<InAppNotification>().Identity(n => n.Id);
});

// Register multi-tenancy services
builder.Services.AddScoped<ITenantContext, TenantContext>();

// Register AWS services (SES for email, SNS for SMS)
var awsRegion = builder.Configuration["AwsNotification:AwsRegion"] ?? "ap-southeast-1";
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IAmazonSimpleEmailService>(_ =>
        new AmazonSimpleEmailServiceClient(RegionEndpoint.GetBySystemName(awsRegion)));
    builder.Services.AddSingleton<IAmazonSimpleNotificationService>(_ =>
        new AmazonSimpleNotificationServiceClient(RegionEndpoint.GetBySystemName(awsRegion)));
}

// Register authentication services
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<OtpService>();
builder.Services.AddSingleton<OtpNotificationService>();
builder.Services.AddScoped<BookingNotificationService>();

// Register booking action token and email notification services
builder.Services.Configure<BookingActionTokenOptions>(
    builder.Configuration.GetSection(BookingActionTokenOptions.SectionName));
builder.Services.AddSingleton<BookingActionTokenService>();
builder.Services.AddScoped<BookingEmailNotificationService>();

// Register background jobs
builder.Services.Configure<BackgroundJobOptions>(
    builder.Configuration.GetSection(BackgroundJobOptions.SectionName));
builder.Services.AddHostedService<SubscriptionExpiryJob>();
builder.Services.AddHostedService<UsageStatisticsResetJob>();

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
        policy.RequireAssertion(context =>
            context.User.HasClaim(c => c.Type == "TenantId") ||
            context.User.IsInRole("GlobalAdmin")));
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

// Seed Global Admin user (idempotent)
await GlobalAdminSeeder.SeedAsync(app.Services);

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// HTTPS redirect disabled — ALB terminates SSL in production
if (!app.Environment.IsDevelopment() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORT")))
{
    app.UseHttpsRedirection();
}

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
