using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using FastEndpoints;
using Marten;
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
});

// Register multi-tenancy services
builder.Services.AddScoped<ITenantContext, TenantContext>();

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

// JWT Authentication will be configured here as auth is implemented
// app.UseAuthentication();
// app.UseAuthorization();

// Use FastEndpoints
app.UseFastEndpoints();

// Request logging
app.UseSerilogRequestLogging();

app.Run();
