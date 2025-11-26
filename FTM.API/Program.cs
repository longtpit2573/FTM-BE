using FTM.API.Controllers;
using FTM.API.Extensions;
using FTM.Application.Hubs;
using FTM.Application.Services;
using FTM.Domain.Entities.Identity;
using FTM.Infrastructure.Data;
using FTM.Infrastructure.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using System.Net;
using System.Net.Mail;
using System.Text;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddIdentityAppDbContext();
builder.Services.AddFTMDbContext();
builder.Services.AddAuthenConfig();
builder.Services.AddDI();
builder.Services.AddPayOSServices(builder.Configuration); // Add PayOS services
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        // Example: ignore circular references
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;

        // Example: don�t preserve object references ($id/$ref will not appear)
        options.SerializerSettings.PreserveReferencesHandling = Newtonsoft.Json.PreserveReferencesHandling.None;

        // Example: use camelCase for property names
        options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();

        //Example: To make your API send and receive enum names like "View", "Edit", "Delete"
        options.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerDocumentation();
builder.Services.AddSwaggerGenNewtonsoftSupport();
// Add HealthChecks for both DbContexts
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppIdentityDbContext>("IdentityDb")
    .AddDbContextCheck<FTMDbContext>("FTMDb");

// OpenTelemetry Tracing
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .SetResourceBuilder(OpenTelemetry.Resources.ResourceBuilder
                .CreateDefault()
                .AddService("ftm-backend"))
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = (httpContext) => 
                {
                    // Don't trace health checks and metrics endpoints
                    return !httpContext.Request.Path.StartsWithSegments("/health") &&
                           !httpContext.Request.Path.StartsWithSegments("/metrics");
                };
            })
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation(options =>
            {
                options.SetDbStatementForText = true;
                options.RecordException = true;
            })
            .AddOtlpExporter(otlpOptions =>
            {
                // Export to Tempo via OTLP gRPC
                var tempoEndpoint = builder.Configuration["OpenTelemetry:Endpoint"] 
                    ?? "http://tempo.monitoring.svc.cluster.local:4317";
                otlpOptions.Endpoint = new Uri(tempoEndpoint);
                otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
            });
    });

var app = builder.Build();
Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");

// PathBase is handled by Ingress, not needed here
// app.UsePathBase("/api");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseLoggerMiddleware();
app.UseGlobalExceptionMiddleware();
app.UseCors("AllowPorts");
// Disable HTTPS redirection in Kubernetes - Ingress handles TLS
// app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseFTAuthorizationMiddleware();

// Prometheus Metrics - expose /metrics endpoint
app.UseRouting();
app.UseHttpMetrics();  // Track HTTP request metrics automatically

// Map Health Check endpoint
app.MapHealthChecks("/health");

// Prometheus Metrics endpoint
app.MapMetrics();  // Expose /metrics for Prometheus scraping

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notification");

// Seed data on startup with better error handling
try
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Starting data seeding...");
    await app.Services.SeedDataAsync();
    logger.LogInformation("Data seeding completed!");
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Failed to seed data on startup");
    // Don't throw - let app continue running
}
app.Run();
