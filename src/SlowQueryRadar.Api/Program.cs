//src/SlowQueryRadar.Api/Program.cs
using Serilog;
using MudBlazor.Services;
using SlowQueryRadar.Analyzer;
using SlowQueryRadar.Api.Components;
using SlowQueryRadar.Api.Services;
using SlowQueryRadar.Benchmark;
using SlowQueryRadar.Collector;
using SlowQueryRadar.Core;
using SlowQueryRadar.Core.Configuration;
using SlowQueryRadar.Profiler;
using SlowQueryRadar.Suggestions;

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")))
{
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.Sources.Clear();
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddSharedAppSettings()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables();

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddControllers();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

builder.Services.AddSlowQueryRadarCollector(builder.Configuration);
builder.Services.AddSlowQueryRadarProfiler(builder.Configuration);
builder.Services.AddSlowQueryRadarAnalyzer();
builder.Services.AddSlowQueryRadarSuggestions(builder.Configuration);
builder.Services.AddSlowQueryRadarBenchmark(builder.Configuration);

var app = builder.Build();

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
app.UseAntiforgery();
app.MapControllers();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program
{
}
