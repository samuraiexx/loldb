using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Configure Application Insights
builder.Services
    .AddApplicationInsightsTelemetryWorkerService(options => { options.EnableAdaptiveSampling = false; })
    .ConfigureFunctionsApplicationInsights()
    .ConfigureApplicationLogging();

// Configure application services
builder.Services
    .AddDataLakeStorage()
    .AddRiotApiServices()
    .AddDataServices();

builder.Build().Run();
