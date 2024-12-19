using Azure.Monitor.OpenTelemetry.AspNetCore;
using AzureAiSearchWebsiteCrawler.Services;
using AzureAiSearchWebsiteCrawler.Utilities.Chunking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Collections.Concurrent;


var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "[HH:mm:ss] ";
    options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
});

if (OperatingSystem.IsLinux())
{
    builder.Logging.AddSystemdConsole(options =>
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "[HH:mm:ss] ";
    });
}

builder.Services.ConfigureOptions<AzureOpenAiOptions>(builder.Configuration);
builder.Services.ConfigureOptions<AzureAiSearchOptions>(builder.Configuration);
builder.Services.ConfigureOptions<WebCrawlerOptions>(builder.Configuration);
builder.Services.ConfigureOptions<TextSplitterOptions>(builder.Configuration);


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
    });

var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}

// Use the OTLP exporter if the endpoint is configured.
var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
if (useOtlpExporter)
{
    builder.Services.Configure<OpenTelemetryLoggerOptions>(logging => logging.AddOtlpExporter());
    builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddOtlpExporter());
    builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddOtlpExporter());
}


builder.Services.AddSingleton<WebCrawlerService>();
builder.Services.AddSingleton<AzureAiSearchService>();
builder.Services.AddSingleton<AzureOpenAiService>();
builder.Services.AddSingleton<ITextSplitter, SentenceTextSplitter>();
builder.Services.AddSingleton<BlockingCollection<WebPageContent>>();
builder.Services.AddHostedService<ApplicationStartupService>();
builder.Services.AddHostedService<BatchProcessingService>();


var host = builder.Build();
await host.RunAsync();
