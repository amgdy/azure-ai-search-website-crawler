using Azure.Monitor.OpenTelemetry.AspNetCore;
using AzureAiSearchWebsiteCrawler.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;


var builder = Host.CreateApplicationBuilder(args);

builder.Services.ConfigureOptions<AzureOpenAiOptions>(builder.Configuration);
builder.Services.ConfigureOptions<AzureAiSearchOptions>(builder.Configuration);
builder.Services.ConfigureOptions<WebCrawlerOptions>(builder.Configuration);

builder.Services.AddOptionsWithValidateOnStart<AzureOpenAiOptions>().Configure(options =>
{

});

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(c => c.AddService("AzureAiSearchWebsiteCrawler"))
    .WithMetrics(metrics =>
    {
        metrics.AddHttpClientInstrumentation()
               .AddRuntimeInstrumentation();
    })
    .WithTracing(tracing =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // We want to view all traces in development
            tracing.SetSampler(new AlwaysOnSampler());
        }

        tracing.AddHttpClientInstrumentation();
        tracing.AddSource(ApplicationStartupService.ActivitySourceName);
    })
    .UseAzureMonitor();

// Use the OTLP exporter if the endpoint is configured.
var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
if (useOtlpExporter)
{
    builder.Services.Configure<OpenTelemetryLoggerOptions>(logging => logging.AddOtlpExporter());
    builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddOtlpExporter());
    builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddOtlpExporter());
}


builder.Services.AddTransient<WebCrawlerService>();
builder.Services.AddTransient<AzureAiSearchService>();
builder.Services.AddTransient<AzureOpenAiService>();

builder.Services.AddHostedService<ApplicationStartupService>();


var host = builder.Build();
await host.RunAsync();
