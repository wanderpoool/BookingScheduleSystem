using Amazon.Runtime;
using BookingScheduleSystem.Web.Components;
using BookingScheduleSystem.Web.Infrastructure.Telemetry;
using BookingScheduleSystem.Web.Services;
using BookingScheduleSystem.Web.Services.Chatbot;
using MudBlazor.Services;
using MudBlazor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Exporter;
using OpenTelemetry.Extensions.AWS.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Configure OpenTelemetry
var otelServiceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "BookingScheduleSystem.Web";
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
            .AddHttpClientInstrumentation();

        if (!string.IsNullOrEmpty(otelExporterEndpoint) && isProduction)
        {
            // Production: 100% sampling (low-traffic app), export to X-Ray via OTLP with SigV4 signing
            tracing.SetSampler(new AlwaysOnSampler());
            tracing.AddOtlpExporter(opts =>
            {
                opts.Endpoint = new Uri(otelExporterEndpoint);
                opts.Protocol = OtlpExportProtocol.HttpProtobuf;
                opts.ExportProcessorType = ExportProcessorType.Batch;
                opts.BatchExportProcessorOptions = new BatchExportProcessorOptions<System.Diagnostics.Activity>
                {
                    ExporterTimeoutMilliseconds = 10_000,
                };
                opts.HttpClientFactory = () =>
                {
                    var credentials = FallbackCredentialsFactory.GetCredentials();
                    var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "ap-southeast-1";
                    var logger = builder.Services.BuildServiceProvider()
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger<XRaySigV4Handler>();
                    return new HttpClient(new XRaySigV4Handler(credentials, region, logger));
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

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add authentication and authorization services
// Cookie scheme is registered as the default to satisfy the authorization middleware.
// Actual auth state is managed by CustomAuthenticationStateProvider via JWT/local storage.
// IMPORTANT: Disable cookie redirects — we never use server-side cookie auth for login;
// Blazor's AuthorizeRouteView + RedirectToLogin handle the flow via SignalR.
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();

// Bypass HTTP-level auth for Blazor pages — AuthorizeRouteView handles auth
// within the SignalR circuit after the HTML shell is served.
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, BlazorAuthorizationMiddlewareResultHandler>();

// Add MudBlazor services with theme configuration
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopRight;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 4000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});

// Configure HttpClient for API communication with transient retry
builder.Services.AddTransient<TransientRetryHandler>();
builder.Services.AddHttpClient("BookingScheduleAPI", client =>
{
    var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5001";
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
}).AddHttpMessageHandler<TransientRetryHandler>();

// Register typed HttpClient for API calls
builder.Services.AddScoped(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    return httpClientFactory.CreateClient("BookingScheduleAPI");
});

// Register authentication and storage services
builder.Services.AddScoped<ILocalStorageService, LocalStorageService>();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IAdminSubscriptionService, AdminSubscriptionService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<IQrCodeService, QrCodeService>();
builder.Services.AddScoped<ITenantSlugService, TenantSlugService>();
builder.Services.AddScoped<ChatbotToolExecutor>();
builder.Services.AddSingleton<ChatbotUsageTracker>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();

// Trust ALB forwarded headers (X-Forwarded-For, X-Forwarded-Proto) so the app
// knows the original scheme was HTTPS and generates correct redirect URLs.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // ALB sits in the VPC — clear default restrictions to trust its headers
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Must be first middleware so all downstream sees the correct scheme/host
app.UseForwardedHeaders();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// HTTPS redirection is handled by the ALB/reverse proxy in production
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithRedirects("/not-found");
app.UseAntiforgery();

app.UseSerilogRequestLogging();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
