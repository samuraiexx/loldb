using Azure;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;

public class AzureDataLakeService
{
  private readonly DataLakeServiceClient _dataLakeServiceClient;
  private readonly ILogger<AzureDataLakeService> _logger;
  private readonly string _fileSystemName;
  private DataLakeFileSystemClient? _fileSystemClient;

  public AzureDataLakeService(DataLakeServiceClient dataLakeServiceClient, ILogger<AzureDataLakeService> logger)
  {
    _dataLakeServiceClient = dataLakeServiceClient;
    _logger = logger;
    _fileSystemName = AppConstants.FileSystemName;
  }

  public async Task<bool> InitializeAsync()
  {
    _logger.LogInformation("Initializing Azure Data Lake service");

    // Create file system if it doesn't exist
    _fileSystemClient = await _dataLakeServiceClient.CreateFileSystemAsync(_fileSystemName);
    if (_fileSystemClient == null)
    {
      _fileSystemClient = _dataLakeServiceClient.GetFileSystemClient(_fileSystemName);
    }

    _logger.LogInformation("File system '{FileSystemName}' ready", _fileSystemName);
    return true;
  }

  private async Task EnsureFileSystemInitializedAsync()
  {
    if (_fileSystemClient == null)
    {
      var initResult = await InitializeAsync();
      if (!initResult || _fileSystemClient == null)
      {
        throw new InvalidOperationException("Failed to initialize Azure Data Lake file system. Check connection string and credentials.");
      }
    }
  }

  private string GetPlayerStatsFilePath(string puuid, string queueType, string region, DateTime timestamp)
  {
    var normalizedQueueType = queueType.ToLowerInvariant().Replace("_", "-");
    var normalizedRegion = region.ToLowerInvariant();
    var timestampStr = timestamp.ToString("yyyyMMdd-HHmmss");
    return $"player-stats/{normalizedQueueType}/{normalizedRegion}/{puuid}/snapshot-{timestampStr}.json";
  }

  private string GetPlayerStatsDirectoryPath(string puuid, string queueType, string region)
  {
    var normalizedQueueType = queueType.ToLowerInvariant().Replace("_", "-");
    var normalizedRegion = region.ToLowerInvariant();
    return $"player-stats/{normalizedQueueType}/{normalizedRegion}/{puuid}/";
  }

  private string GetMatchFilePath(string matchId, string region, string fileType = "match")
  {
    return $"matches/{region}/{matchId}/{fileType}.json";
  }

  private string GetMatchDirectoryPath(string matchId, string region)
  {
    return $"matches/{region}/{matchId}/";
  }

  private string GetMatchesRegionDirectoryPath(string region)
  {
    return $"matches/{region}/";
  }

  private string GetPlayerStatsQueueDirectoryPath(string queueType, string region)
  {
    var normalizedQueueType = queueType.ToLowerInvariant().Replace("_", "-");
    var normalizedRegion = region.ToLowerInvariant();
    return $"player-stats/{normalizedQueueType}/{normalizedRegion}/";
  }

  private string GetMatchesDirectoryPath(string region)
  {
    return GetMatchesRegionDirectoryPath(region);
  }
  public async Task<PlayerStatsDocument?> GetPlayerStatsAsync(string puuid, string queueType, string region)
  {
    try
    {
      await EnsureFileSystemInitializedAsync();

      var playerDirectoryPath = GetPlayerStatsDirectoryPath(puuid, queueType, region);
      var directoryClient = _fileSystemClient!.GetDirectoryClient(playerDirectoryPath);

      if (!await directoryClient.ExistsAsync())
      {
        return null;
      }

      // Get the latest snapshot file
      var latestSnapshot = await GetLatestPlayerSnapshotAsync(directoryClient);
      if (latestSnapshot == null)
      {
        return null;
      }

      var fileClient = _fileSystemClient.GetFileClient(latestSnapshot);
      var downloadResult = await fileClient.ReadAsync();
      using var reader = new StreamReader(downloadResult.Value.Content);
      var json = await reader.ReadToEndAsync();

      return JsonConvert.DeserializeObject<PlayerStatsDocument>(json);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting player stats for {Puuid} in {QueueType} for region {Region}", puuid, queueType, region);
      throw;
    }
  }

  private async Task<string?> GetLatestPlayerSnapshotAsync(DataLakeDirectoryClient directoryClient)
  {
    string? latestFile = null;
    DateTime latestTimestamp = DateTime.MinValue;

    await foreach (var pathItem in directoryClient.GetPathsAsync())
    {
      if (pathItem.IsDirectory == false && pathItem.Name.StartsWith("snapshot-") && pathItem.Name.EndsWith(".json"))
      {
        // Extract timestamp from filename: snapshot-20240627-143022.json
        var fileName = Path.GetFileNameWithoutExtension(pathItem.Name);
        var timestampPart = fileName.Substring("snapshot-".Length);

        if (DateTime.TryParseExact(timestampPart, "yyyyMMdd-HHmmss", null, System.Globalization.DateTimeStyles.None, out var timestamp))
        {
          if (timestamp > latestTimestamp)
          {
            latestTimestamp = timestamp;
            latestFile = $"{directoryClient.Path}/{pathItem.Name}";
          }
        }
      }
    }

    return latestFile;
  }
  public async Task UpsertPlayerStatsAsync(PlayerStatsDocument playerStats, string queueType)
  {
    await EnsureFileSystemInitializedAsync();

    // Use the LastUpdated timestamp, or current time if not set
    var timestamp = playerStats.LastUpdated != default ? playerStats.LastUpdated : DateTime.UtcNow;
    var filePath = GetPlayerStatsFilePath(playerStats.Puuid, queueType, playerStats.Region, timestamp);
    var fileClient = _fileSystemClient!.GetFileClient(filePath);

    var json = JsonConvert.SerializeObject(playerStats, Formatting.None);
    var data = Encoding.UTF8.GetBytes(json);

    await fileClient.UploadAsync(new MemoryStream(data), overwrite: true);

    _logger.LogDebug("Upserted player stats for {Puuid} in {QueueType} at {Timestamp}",
      playerStats.Puuid, queueType, timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
  }

  public async Task BatchUpsertPlayerStatsAsync(List<PlayerStatsDocument> playerStatsList, string queueType, string region)
  {
    if (!playerStatsList.Any())
    {
      _logger.LogDebug("No player stats to batch upsert");
      return;
    }

    await EnsureFileSystemInitializedAsync();

    _logger.LogInformation("Batch upserting {TotalCount} player stats for {QueueType} in {Region}",
        playerStatsList.Count, queueType, region);

    var tasks = new List<Task>();
    var semaphore = new SemaphoreSlim(10, 10); // Limit concurrent operations

    foreach (var playerStats in playerStatsList)
    {
      tasks.Add(Task.Run(async () =>
      {
        await semaphore.WaitAsync();
        try
        {
          await UpsertPlayerStatsAsync(playerStats, queueType);
        }
        finally
        {
          semaphore.Release();
        }
      }));
    }

    await Task.WhenAll(tasks);

    _logger.LogInformation("Batch upsert completed for {QueueType} in {Region}. Processed: {Processed}",
        queueType, region, playerStatsList.Count);
  }
  public async Task<List<string>> GetRankedPuuidsAsync(string queueType, string tier, string division, string region)
  {
    await EnsureFileSystemInitializedAsync();

    var queueDirectoryPath = GetPlayerStatsQueueDirectoryPath(queueType, region);
    var queueDirectoryClient = _fileSystemClient!.GetDirectoryClient(queueDirectoryPath);

    var puuids = new List<string>();

    try
    {
      // Iterate through player directories
      await foreach (var pathItem in queueDirectoryClient.GetPathsAsync())
      {
        if (pathItem.IsDirectory == true)
        {
          try
          {
            var puuid = pathItem.Name;
            var playerDirectoryClient = _fileSystemClient.GetDirectoryClient($"{queueDirectoryPath}{puuid}");

            // Get the latest snapshot for this player
            var latestSnapshot = await GetLatestPlayerSnapshotAsync(playerDirectoryClient);
            if (latestSnapshot != null)
            {
              var fileClient = _fileSystemClient.GetFileClient(latestSnapshot);
              var downloadResult = await fileClient.ReadAsync();
              using var reader = new StreamReader(downloadResult.Value.Content);
              var json = await reader.ReadToEndAsync();

              var playerStats = JsonConvert.DeserializeObject<PlayerStatsDocument>(json);

              if (playerStats?.Snapshot != null &&
                  string.Equals(playerStats.Snapshot.Tier, tier, StringComparison.OrdinalIgnoreCase) &&
                  string.Equals(playerStats.Snapshot.Rank, division, StringComparison.OrdinalIgnoreCase))
              {
                puuids.Add(playerStats.Puuid);
              }
            }
          }
          catch (Exception ex)
          {
            _logger.LogWarning(ex, "Failed to process player directory {DirectoryName}", pathItem.Name);
          }
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error listing player stats directories for {QueueType} in {Region}", queueType, region);
      throw;
    }

    // Randomly shuffle the list
    var random = new Random();
    for (int i = puuids.Count - 1; i > 0; i--)
    {
      int j = random.Next(i + 1);
      (puuids[i], puuids[j]) = (puuids[j], puuids[i]);
    }

    _logger.LogInformation("Retrieved {TotalCount} PUUIDs for rank {Tier} {Division} for {QueueType} in {Region}",
        puuids.Count, tier, division, queueType, region);

    return puuids;
  }
  public async Task<MatchDocument?> GetMatchAsync(string matchId, string region)
  {
    try
    {
      await EnsureFileSystemInitializedAsync();

      var filePath = GetMatchFilePath(matchId, region, "match");
      var fileClient = _fileSystemClient!.GetFileClient(filePath);

      if (!await fileClient.ExistsAsync())
      {
        return null;
      }

      var downloadResult = await fileClient.ReadAsync();
      using var reader = new StreamReader(downloadResult.Value.Content);
      var json = await reader.ReadToEndAsync();

      return JsonConvert.DeserializeObject<MatchDocument>(json);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting match {MatchId} for region {Region}", matchId, region);
      throw;
    }
  }

  public async Task UpsertMatchAsync(MatchDocument match)
  {
    try
    {
      await EnsureFileSystemInitializedAsync();

      var filePath = GetMatchFilePath(match.MatchId, match.Region, "match");
      var fileClient = _fileSystemClient!.GetFileClient(filePath);

      var json = JsonConvert.SerializeObject(match, Formatting.None);
      var data = Encoding.UTF8.GetBytes(json);

      await fileClient.UploadAsync(new MemoryStream(data), overwrite: true);

      _logger.LogDebug("Upserted match {MatchId} for region {Region}", match.MatchId, match.Region);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error upserting match {MatchId} for region {Region}", match.MatchId, match.Region);
      throw;
    }
  }

  public async Task<BatchUpsertResult> BatchUpsertMatchesAsync(List<MatchDocument> matches, string region)
  {
    if (!matches.Any())
    {
      _logger.LogDebug("No matches to batch upsert");
      return new BatchUpsertResult { TotalProcessed = 0, TotalErrors = 0 };
    }

    await EnsureFileSystemInitializedAsync();

    _logger.LogInformation("Batch upserting {TotalCount} matches for region {Region}", matches.Count, region);

    var totalProcessed = 0;
    var totalErrors = 0;
    var tasks = new List<Task>();
    var semaphore = new SemaphoreSlim(20, 20); // Limit concurrent operations

    foreach (var match in matches)
    {
      tasks.Add(Task.Run(async () =>
      {
        await semaphore.WaitAsync();
        try
        {
          await UpsertMatchAsync(match);
          Interlocked.Increment(ref totalProcessed);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Failed to upsert match {MatchId} for region {Region}", match.MatchId, match.Region);
          Interlocked.Increment(ref totalErrors);
        }
        finally
        {
          semaphore.Release();
        }
      }));
    }

    await Task.WhenAll(tasks);

    _logger.LogInformation("Batch upsert completed for region {Region}. Processed: {Processed}, Errors: {Errors}",
        region, totalProcessed, totalErrors);

    return new BatchUpsertResult { TotalProcessed = totalProcessed, TotalErrors = totalErrors };
  }
  public async Task<List<MatchDocument>> GetUnprocessedMatchesAsync(string region, int maxCount = 120, DateTime? maxCreatedTime = null)
  {
    await EnsureFileSystemInitializedAsync();

    var regionDirectoryPath = GetMatchesRegionDirectoryPath(region);
    var regionDirectoryClient = _fileSystemClient!.GetDirectoryClient(regionDirectoryPath);

    var matches = new List<MatchDocument>();
    var processedCount = 0;

    try
    {
      await foreach (var pathItem in regionDirectoryClient.GetPathsAsync())
      {
        if (processedCount >= maxCount)
          break;

        if (pathItem.IsDirectory == true)
        {
          try
          {
            var matchId = pathItem.Name;
            var matchFilePath = GetMatchFilePath(matchId, region, "match");
            var fileClient = _fileSystemClient!.GetFileClient(matchFilePath);

            if (await fileClient.ExistsAsync())
            {
              var downloadResult = await fileClient.ReadAsync();
              using var reader = new StreamReader(downloadResult.Value.Content);
              var json = await reader.ReadToEndAsync();

              var match = JsonConvert.DeserializeObject<MatchDocument>(json);

              if (match != null && !match.Processed)
              {
                if (maxCreatedTime.HasValue && match.CreatedAt > maxCreatedTime.Value)
                  continue;

                matches.Add(match);
                processedCount++;
              }
            }
          }
          catch (Exception ex)
          {
            _logger.LogWarning(ex, "Failed to process match directory {DirectoryName}", pathItem.Name);
          }
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error listing match directories for region {Region}", region);
      throw;
    }

    _logger.LogInformation("Retrieved {Count} unprocessed matches for region {Region} (maxCreatedTime: {MaxCreatedTime})",
        matches.Count, region, maxCreatedTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "none");

    return matches;
  }
  public async Task DeleteMatchAsync(string matchId, string region)
  {
    try
    {
      await EnsureFileSystemInitializedAsync();

      var matchDirectoryPath = GetMatchDirectoryPath(matchId, region);
      var directoryClient = _fileSystemClient!.GetDirectoryClient(matchDirectoryPath);

      if (await directoryClient.ExistsAsync())
      {
        await directoryClient.DeleteAsync();
        _logger.LogInformation("Deleted match directory {MatchId} from region {Region}", matchId, region);
      }
      else
      {
        _logger.LogWarning("Match directory {MatchId} not found in data lake for region {Region}", matchId, region);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error deleting match {MatchId} from region {Region}", matchId, region);
      throw;
    }
  }

  /// <summary>
  /// Gets all historical snapshots for a player within a date range
  /// </summary>
  public async Task<List<PlayerStatsDocument>> GetPlayerStatsHistoryAsync(string puuid, string queueType, string region, DateTime? fromDate = null, DateTime? toDate = null)
  {
    try
    {
      await EnsureFileSystemInitializedAsync();

      var playerDirectoryPath = GetPlayerStatsDirectoryPath(puuid, queueType, region);
      var directoryClient = _fileSystemClient!.GetDirectoryClient(playerDirectoryPath);

      if (!await directoryClient.ExistsAsync())
      {
        return new List<PlayerStatsDocument>();
      }

      var snapshots = new List<(DateTime timestamp, string filePath, PlayerStatsDocument data)>();

      await foreach (var pathItem in directoryClient.GetPathsAsync())
      {
        if (pathItem.IsDirectory == false && pathItem.Name.StartsWith("snapshot-") && pathItem.Name.EndsWith(".json"))
        {
          // Extract timestamp from filename: snapshot-20240627-143022.json
          var fileName = Path.GetFileNameWithoutExtension(pathItem.Name);
          var timestampPart = fileName.Substring("snapshot-".Length);

          if (DateTime.TryParseExact(timestampPart, "yyyyMMdd-HHmmss", null, System.Globalization.DateTimeStyles.None, out var timestamp))
          {
            // Apply date filters if specified
            if (fromDate.HasValue && timestamp < fromDate.Value) continue;
            if (toDate.HasValue && timestamp > toDate.Value) continue;

            try
            {
              var fileClient = _fileSystemClient.GetFileClient($"{playerDirectoryPath}{pathItem.Name}");
              var downloadResult = await fileClient.ReadAsync();
              using var reader = new StreamReader(downloadResult.Value.Content);
              var json = await reader.ReadToEndAsync();

              var playerStats = JsonConvert.DeserializeObject<PlayerStatsDocument>(json);
              if (playerStats != null)
              {
                snapshots.Add((timestamp, pathItem.Name, playerStats));
              }
            }
            catch (Exception ex)
            {
              _logger.LogWarning(ex, "Failed to read snapshot file {FileName} for player {Puuid}", pathItem.Name, puuid);
            }
          }
        }
      }

      // Sort by timestamp (oldest first)
      return snapshots
        .OrderBy(s => s.timestamp)
        .Select(s => s.data)
        .ToList();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting player stats history for {Puuid} in {QueueType} for region {Region}", puuid, queueType, region);
      throw;
    }
  }

  public async Task UpsertMatchTimelineAsync(string matchId, string region, object timelineData)
  {
    await EnsureFileSystemInitializedAsync();

    var normalizedRegion = region.ToLowerInvariant();
    var filePath = $"matches/{normalizedRegion}/{matchId}/timeline.json";

    try
    {
      _logger.LogDebug("Upserting match timeline data for match {MatchId} in region {Region}", matchId, region);

      var json = JsonConvert.SerializeObject(timelineData, Formatting.None);
      var data = Encoding.UTF8.GetBytes(json);

      var fileClient = _fileSystemClient!.GetFileClient(filePath);
      await fileClient.UploadAsync(new MemoryStream(data), overwrite: true);

      _logger.LogDebug("Successfully upserted match timeline data for match {MatchId} in region {Region}", matchId, region);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error upserting match timeline data for match {MatchId} in region {Region}", matchId, region);
      throw;
    }
  }

  public async Task<object?> GetMatchTimelineAsync(string matchId, string region)
  {
    await EnsureFileSystemInitializedAsync();

    var normalizedRegion = region.ToLowerInvariant();
    var filePath = $"matches/{normalizedRegion}/{matchId}/timeline.json";

    try
    {
      _logger.LogDebug("Getting match timeline data for match {MatchId} in region {Region}", matchId, region);

      var fileClient = _fileSystemClient!.GetFileClient(filePath);
      var response = await fileClient.ReadAsync();

      if (response?.Value != null)
      {
        using var reader = new StreamReader(response.Value.Content);
        var json = await reader.ReadToEndAsync();
        var timelineData = JsonConvert.DeserializeObject<object>(json);

        _logger.LogDebug("Successfully retrieved match timeline data for match {MatchId} in region {Region}", matchId, region);
        return timelineData;
      }
    }
    catch (RequestFailedException ex) when (ex.Status == 404)
    {
      _logger.LogDebug("Match timeline data not found for match {MatchId} in region {Region}", matchId, region);
      return null;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting match timeline data for match {MatchId} in region {Region}", matchId, region);
      throw;
    }

    return null;
  }
}
