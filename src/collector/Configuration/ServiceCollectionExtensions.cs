using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Azure.Storage.Files.DataLake;

/// <summary>
/// Extension methods for configuring application services
/// </summary>
public static class ServiceCollectionExtensions
{
  /// <summary>
  /// Adds Azure Data Lake Storage client configuration
  /// </summary>
  public static IServiceCollection AddDataLakeStorage(this IServiceCollection services)
  {
    services.AddSingleton<DataLakeServiceClient>(serviceProvider =>
    {
      var connectionString = Environment.GetEnvironmentVariable(AppConstants.EnvironmentVariables.AzureStorageConnectionString) ??
                            throw new InvalidOperationException($"{AppConstants.EnvironmentVariables.AzureStorageConnectionString} environment variable is required");

      return new DataLakeServiceClient(connectionString);
    });

    return services;
  }

  /// <summary>
  /// Adds Riot API service configuration
  /// </summary>
  public static IServiceCollection AddRiotApiServices(this IServiceCollection services)
  {
    services.AddHttpClient<RiotApiService>();
    services.AddScoped<RiotApiService>();

    return services;
  }

  /// <summary>
  /// Adds Azure Data Lake service configuration
  /// </summary>
  public static IServiceCollection AddDataServices(this IServiceCollection services)
  {
    services.AddScoped<AzureDataLakeService>();

    return services;
  }

  /// <summary>
  /// Configures logging with Application Insights
  /// </summary>
  public static IServiceCollection ConfigureApplicationLogging(this IServiceCollection services)
  {
    services.Configure<LoggerFilterOptions>(options =>
    {
      // The Application Insights SDK adds a default logging filter that instructs ILogger to capture only Warning and more severe logs. 
      // Application Insights requires an explicit override.
      LoggerFilterRule? defaultRule = options.Rules.FirstOrDefault(rule =>
        rule.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");

      if (defaultRule is not null)
      {
        options.Rules.Remove(defaultRule);
      }

      options.MinLevel = LogLevel.Information; // Set minimum log level to Information
    });

    return services;
  }
}
