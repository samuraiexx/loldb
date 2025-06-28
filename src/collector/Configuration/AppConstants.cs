/// <summary>
/// Application constants
/// </summary>
public static class AppConstants
{
  /// <summary>
  /// Azure Data Lake file system name
  /// </summary>
  public const string FileSystemName = "loldb-data";

  /// <summary>
  /// Environment variable names
  /// </summary>
  public static class EnvironmentVariables
  {
    public const string AzureStorageConnectionString = "ADL_CONNECTION_STRING";
    public const string DataServiceType = "DATA_SERVICE_TYPE";
    public const string RiotApiKey = "RIOT_API_KEY";
    public const string ApplicationInsightsConnectionString = "APPLICATIONINSIGHTS_CONNECTION_STRING";
  }

  /// <summary>
  /// Data Lake Storage paths
  /// </summary>
  public static class DataLakePaths
  {
    public const string PlayerStats = "player-stats";
    public const string Matches = "matches";
    public const string MatchFile = "match.json";
    public const string TimelineFile = "timeline.json";
  }

  /// <summary>
  /// Default configuration values
  /// </summary>
  public static class Defaults
  {
    public const int MaxConcurrentOperations = 20;
    public const int DefaultMaxMatches = 120;
    public const string DefaultDataServiceType = "datalake";
  }
}
