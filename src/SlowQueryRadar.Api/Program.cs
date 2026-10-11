//src/SlowQueryRadar.Api/Program.cs
using Serilog;
using MudBlazor.Services;
using SlowQueryRadar.Analyzer;
using SlowQueryRadar.Api.Components;
using SlowQueryRadar.Api.Services;
using SlowQueryRadar.Benchmark;
using SlowQueryRadar.Collector;
using SlowQueryRadar.Core;
using SlowQueryRadar.Profiler;
using SlowQueryRadar.Suggestions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddControllers();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddSingleton<QueryStateStore>();
builder.Services.AddHttpClient<QueriesApiClient>(client =>
{
    var selfUrl = builder.Configuration["SelfUrl"] ?? "https://localhost:7001/";
    client.BaseAddress = new Uri(selfUrl, UriKind.Absolute);
});
builder.Services.AddScoped<IQueriesApiClient>(provider =>
    provider.GetRequiredService<QueriesApiClient>());

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