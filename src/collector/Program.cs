using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using collector.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Configure services
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Logging.Services.Configure<LoggerFilterOptions>(options =>
    {
        // The Application Insights SDK adds a default logging filter that instructs ILogger to capture only Warning and more severe logs. Application Insights requires an explicit override.
        // Log levels can also be configured using appsettings.json. For more information, see https://learn.microsoft.com/azure/azure-monitor/app/worker-service#ilogger-logs
        LoggerFilterRule? defaultRule = options.Rules.FirstOrDefault(rule => rule.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");

        if (defaultRule is not null)
        {
            options.Rules.Remove(defaultRule);
        }

        // Remove any generic rule that sets the minimum level to Warning
        LoggerFilterRule? genericWarningRule = options.Rules.FirstOrDefault(rule => rule.ProviderName is null && rule.CategoryName is null && rule.LogLevel == LogLevel.Warning);

        if (genericWarningRule is not null)
        {
            options.Rules.Remove(genericWarningRule);
        }

        // Ensure Information logs are captured
        options.MinLevel = LogLevel.Information;
    });

// Add HTTP client
builder.Services.AddHttpClient<IRiotApiService, RiotApiService>();

// Add Cosmos DB client
builder.Services.AddSingleton<CosmosClient>(serviceProvider =>
{
  var cosmosEndpoint = Environment.GetEnvironmentVariable("AZURE_COSMOS_ENDPOINT") ??
                      throw new InvalidOperationException("AZURE_COSMOS_ENDPOINT environment variable is required");
  var cosmosKey = Environment.GetEnvironmentVariable("AZURE_COSMOS_KEY") ??
                 throw new InvalidOperationException("AZURE_COSMOS_KEY environment variable is required");

  return new CosmosClient(cosmosEndpoint, cosmosKey);
});

// Register services
builder.Services.AddScoped<IRiotApiService, RiotApiService>();
builder.Services.AddScoped<ICosmosDbService, CosmosDbService>();

builder.Build().Run();
