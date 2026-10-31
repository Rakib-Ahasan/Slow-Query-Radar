//src/SlowQueryRadar.Api/Program.cs
using Serilog;
using Microsoft.Data.SqlClient;
using MudBlazor.Services;
using SlowQueryRadar.Analyzer;
using SlowQueryRadar.Api.Components;
using SlowQueryRadar.Api.Services;
using SlowQueryRadar.Benchmark;
using SlowQueryRadar.Collector;
using SlowQueryRadar.Core;
using SlowQueryRadar.Core.Abstractions;
using SlowQueryRadar.Core.Configuration;
using SlowQueryRadar.Core.Models;
using SlowQueryRadar.Profiler;
using SlowQueryRadar.Suggestions;
using Scalar.AspNetCore;
using System.Threading.RateLimiting;
using SlowQueryRadar.Api.Health;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")))
{
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.Sources.Clear();
builder.Configuration.SetBasePath(AppContext.BaseDirectory);
SharedConfiguration.AddSharedSettings(builder.Configuration);
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables();

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddCheck<SnapshotStoreHealthCheck>("snapshot-store", tags: ["ready"])
    .AddCheck<TargetAvailabilityHealthCheck>("targets", tags: ["ready"]);
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddPolicy("ApiCors", policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Api:Cors:Origins").Get<string[]>() ?? ["http://localhost:5017"])
        .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("benchmarks", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});
builder.Services.AddSingleton<BenchmarkJobStore>();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddSingleton<DemoDataService>();

builder.Services.AddSlowQueryRadarCollector(builder.Configuration);
builder.Services.AddSlowQueryRadarProfiler(builder.Configuration);
builder.Services.AddSlowQueryRadarAnalyzer();
builder.Services.AddSlowQueryRadarSuggestions(builder.Configuration);
builder.Services.AddSlowQueryRadarBenchmark(builder.Configuration);
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Notifications"));
builder.Services.AddHttpClient(nameof(WebhookNotifier));
builder.Services.AddSingleton<INotifier, WebhookNotifier>();
builder.Services.AddHostedService<CollectionScheduler>();

var queryStoreConnectionString = builder.Configuration["QueryStore:ConnectionString"];
if (string.IsNullOrWhiteSpace(queryStoreConnectionString))
{
    throw new InvalidOperationException("QueryStore:ConnectionString is not configured. Copy appsettings.Shared.template.json to appsettings.Shared.json and set QueryStore:ConnectionString.");
}

var app = builder.Build();
app.Logger.LogInformation("Configured Query Store SQL target {ConnectionSummary}", Program.DescribeConnection(queryStoreConnectionString));

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    context.Response.ContentType = "application/problem+json";
    context.Response.StatusCode = exception switch
    {
        QueryStoreDisabledException => StatusCodes.Status503ServiceUnavailable,
        UnsafeQueryException => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status500InternalServerError
    };
    await context.Response.WriteAsJsonAsync(new
    {
        title = exception switch
        {
            QueryStoreDisabledException => "Query Store unavailable",
            UnsafeQueryException => "Unsafe query",
            _ => "An unexpected error occurred"
        },
        detail = exception?.Message
    });
}));

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("ApiCors");
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    var requiredKey = app.Configuration["Api:ApiKey"];
    if (!app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(requiredKey)
        && (!context.Request.Headers.TryGetValue("X-Api-Key", out var provided) || provided != requiredKey))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new ProblemDetails { Title = "API key required", Status = 401 });
        return;
    }
    await next();
});
app.UseAntiforgery();
app.MapControllers();
app.MapHealthChecks("/api/v1/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/api/v1/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapOpenApi();
app.MapScalarApiReference();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program
{
    internal static string DescribeConnection(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "<empty>";
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var server = string.IsNullOrWhiteSpace(builder.DataSource) ? "<unknown>" : builder.DataSource;
            var database = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "<default>" : builder.InitialCatalog;
            var password = string.IsNullOrWhiteSpace(builder.Password) ? "<not-set>" : "***";
            return $"Server={server};Database={database};Password={password}";
        }
        catch (ArgumentException)
        {
            return "<invalid connection string>";
        }
    }
}
