using BookingScheduleSystem.Web.Components;
using BookingScheduleSystem.Web.Services;
using MudBlazor.Services;
using MudBlazor;
using Microsoft.AspNetCore.Components.Authorization;
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
            .AddAspNetCoreInstrumentation(opts =>
            {
                opts.RecordException = true;
            })
            .AddHttpClientInstrumentation();

        if (!string.IsNullOrEmpty(otelExporterEndpoint))
        {
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

        if (!string.IsNullOrEmpty(otelExporterEndpoint))
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
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();

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

// Configure HttpClient for API communication
builder.Services.AddHttpClient("BookingScheduleAPI", client =>
{
    var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5001";
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

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
builder.Services.AddScoped<IQrCodeService, QrCodeService>();
builder.Services.AddScoped<ITenantSlugService, TenantSlugService>();

var app = builder.Build();

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
