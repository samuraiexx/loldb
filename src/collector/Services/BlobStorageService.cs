using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using collector.Models;

namespace collector.Services;

public interface IBlobStorageService
{
  Task<MatchCollectionConfig> GetMatchCollectionConfigAsync();
  Task SaveMatchCollectionConfigAsync(MatchCollectionConfig config);
}

public class BlobStorageService : IBlobStorageService
{
  private readonly BlobServiceClient _blobServiceClient;
  private readonly ILogger<BlobStorageService> _logger;
  private readonly string _containerName = "match-collection-config";
  private readonly string _configBlobName = "match-collection-start-time.json";

  public BlobStorageService(BlobServiceClient blobServiceClient, ILogger<BlobStorageService> logger)
  {
    _blobServiceClient = blobServiceClient;
    _logger = logger;
  }

  public async Task<MatchCollectionConfig> GetMatchCollectionConfigAsync()
  {
    try
    {
      var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
      await containerClient.CreateIfNotExistsAsync();

      var blobClient = containerClient.GetBlobClient(_configBlobName);

      if (!await blobClient.ExistsAsync())
      {
        _logger.LogInformation("Match collection config blob does not exist, creating default config");
        var defaultConfig = new MatchCollectionConfig();
        await SaveMatchCollectionConfigAsync(defaultConfig);
        return defaultConfig;
      }

      var response = await blobClient.DownloadContentAsync();
      var content = response.Value.Content.ToString();
      var config = JsonConvert.DeserializeObject<MatchCollectionConfig>(content);

      _logger.LogInformation("Retrieved match collection config with start time: {StartTime}", config?.StartTime);
      return config ?? new MatchCollectionConfig();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error retrieving match collection config");
      return new MatchCollectionConfig();
    }
  }

  public async Task SaveMatchCollectionConfigAsync(MatchCollectionConfig config)
  {
    try
    {
      var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
      await containerClient.CreateIfNotExistsAsync();

      var blobClient = containerClient.GetBlobClient(_configBlobName);
      var json = JsonConvert.SerializeObject(config, Formatting.Indented);

      await blobClient.UploadAsync(BinaryData.FromString(json), overwrite: true);

      _logger.LogInformation("Saved match collection config with start time: {StartTime}", config.StartTime);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error saving match collection config");
      throw;
    }
  }
}
