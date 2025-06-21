using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

public interface ICosmosDbService
{
  Task<PlayerStatsDocument?> GetPlayerStatsAsync(string puuid, string queueType, string region);
  Task UpsertPlayerStatsAsync(PlayerStatsDocument playerStats, string queueType);
  Task BatchUpsertPlayerStatsAsync(List<PlayerStatsDocument> playerStatsList, string queueType, string region);
  Task<bool> InitializeAsync();
  Task<List<string>> GetRankedPuuidsAsync(string queueType, string tier, string division);
  Task<MatchDocument?> GetMatchAsync(string matchId, string region);
  Task UpsertMatchAsync(MatchDocument match);
  Task BatchUpsertMatchesAsync(List<MatchDocument> matches, string region);
  Task<List<MatchDocument>> GetUnprocessedMatchesAsync(string region, int maxCount = 120, DateTime? maxCreatedTime = null);
}

public class CosmosDbService : ICosmosDbService
{
  private readonly CosmosClient _cosmosClient;
  private readonly ILogger<CosmosDbService> _logger;
  private readonly string _databaseName;
  private Database? _database;
  private readonly Dictionary<string, Container> _containers = new();

  public CosmosDbService(CosmosClient cosmosClient, ILogger<CosmosDbService> logger)
  {
    _cosmosClient = cosmosClient;
    _logger = logger;
    _databaseName = "player_stats";
  }
  public async Task<bool> InitializeAsync()
  {
    try
    {
      _logger.LogInformation("Initializing Cosmos DB service");

      // First validate the connection
      try
      {
        await _cosmosClient.ReadAccountAsync();
        _logger.LogInformation("Cosmos DB connection validated successfully");
      }
      catch (Exception connEx)
      {
        _logger.LogError(connEx, "Failed to connect to Cosmos DB. Check endpoint and credentials.");
        return false;
      }

      // Create database if it doesn't exist
      var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName);
      _database = databaseResponse.Database;

      _logger.LogInformation("Database '{DatabaseName}' ready", _databaseName);
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to initialize Cosmos DB service. Error: {ErrorMessage}", ex.Message);
      return false;
    }
  }
  private async Task<Container> GetContainerAsync(string queueType)
  {
    if (_containers.TryGetValue(queueType, out var existingContainer))
    {
      return existingContainer;
    }

    if (_database == null)
    {
      _logger.LogInformation("Database not initialized, initializing now...");
      var initResult = await InitializeAsync();

      if (!initResult || _database == null)
      {
        throw new InvalidOperationException("Failed to initialize Cosmos DB database. Check connection string and credentials.");
      }
    }

    _logger.LogInformation("Creating container for queue type: {QueueType}", queueType);

    var containerResponse = await _database.CreateContainerIfNotExistsAsync(
        id: queueType,
        partitionKeyPath: "/region");

    var container = containerResponse.Container;
    _containers[queueType] = container;

    _logger.LogInformation("Container '{ContainerName}' ready", queueType);
    return container;
  }
  public async Task<PlayerStatsDocument?> GetPlayerStatsAsync(string puuid, string queueType, string region)
  {
    try
    {
      var container = await GetContainerAsync(queueType);
      var response = await container.ReadItemAsync<PlayerStatsDocument>(puuid, new PartitionKey(region));
      return response.Resource;
    }
    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
      return null;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting player stats for {Puuid} in {QueueType} for region {Region}", puuid, queueType, region);
      throw;
    }
  }
  public async Task UpsertPlayerStatsAsync(PlayerStatsDocument playerStats, string queueType)
  {
    try
    {
      var container = await GetContainerAsync(queueType);
      await container.UpsertItemAsync(playerStats, new PartitionKey(playerStats.Region));
      _logger.LogDebug("Upserted player stats for {Puuid} in {QueueType}", playerStats.Puuid, queueType);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error upserting player stats for {Puuid} in {QueueType}", playerStats.Puuid, queueType);
      throw;
    }
  }

  public async Task BatchUpsertPlayerStatsAsync(List<PlayerStatsDocument> playerStatsList, string queueType, string region)
  {
    if (!playerStatsList.Any())
    {
      _logger.LogDebug("No player stats to batch upsert");
      return;
    }

    var container = await GetContainerAsync(queueType);
    const int batchSize = 50; // Cosmos DB batch operation limit / 2
    var batches = playerStatsList
        .Select((item, index) => new { item, index })
        .GroupBy(x => x.index / batchSize)
        .Select(g => g.Select(x => x.item).ToList())
        .ToList();

    _logger.LogInformation("Batch upserting {TotalCount} player stats in {BatchCount} batches for {QueueType} in {Region}",
        playerStatsList.Count, batches.Count, queueType, region);

    var totalProcessed = 0;
    var totalErrors = 0;

    foreach (var batch in batches)
    {
      var transactionalBatch = container.CreateTransactionalBatch(new PartitionKey(region));

      foreach (var playerStats in batch)
      {
        transactionalBatch.UpsertItem(playerStats);
      }

      var batchResponse = await transactionalBatch.ExecuteAsync();

      if (batchResponse.IsSuccessStatusCode)
      {
        totalProcessed += batch.Count;
        _logger.LogDebug("Successfully batch upserted {Count} player stats for {QueueType} in {Region}",
            batch.Count, queueType, region);
      }
      else
      {
        _logger.LogError("Batch upsert failed with status code: {StatusCode} for {QueueType} in {Region}",
            batchResponse.StatusCode, queueType, region);
        totalErrors += batch.Count;
      }
    }

    _logger.LogInformation("Batch upsert completed for {QueueType} in {Region}. Processed: {Processed}, Errors: {Errors}",
        queueType, region, totalProcessed, totalErrors);

    if (totalErrors > 0)
    {
      throw new Exception($"Batch upsert completed with {totalErrors} errors for {queueType} in {region}");
    }
  }

  public async Task<List<string>> GetRankedPuuidsAsync(string queueType, string tier, string division)
  {
    try
    {
      var container = await GetContainerAsync(queueType);

      var queryDefinition = new QueryDefinition(
        "SELECT c.puuid FROM c WHERE c.snapshot != null AND UPPER(c.snapshot.tier) = @tier AND UPPER(c.snapshot.rank) = @division")
        .WithParameter("@tier", tier.ToUpper())
        .WithParameter("@division", division.ToUpper());

      var puuids = new List<string>();
      var iterator = container.GetItemQueryIterator<dynamic>(queryDefinition);

      while (iterator.HasMoreResults)
      {
        var response = await iterator.ReadNextAsync();
        foreach (var item in response)
        {
          if (item?.puuid != null)
          {
            puuids.Add((string)item.puuid);
          }
        }
      }

      _logger.LogInformation("Retrieved {Count} PUUIDs for exact rank {Tier} {Division} for {QueueType}",
          puuids.Count, tier, division, queueType);

      return puuids;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting ranked PUUIDs for {QueueType}", queueType);
      throw;
    }
  }

  public async Task<MatchDocument?> GetMatchAsync(string matchId, string region)
  {
    try
    {
      var container = await GetContainerAsync("matches");
      var response = await container.ReadItemAsync<MatchDocument>(matchId, new PartitionKey(region));
      return response.Resource;
    }
    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
      return null;
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
      var container = await GetContainerAsync("matches");
      await container.UpsertItemAsync(match, new PartitionKey(match.Region));
      _logger.LogDebug("Upserted match {MatchId} for region {Region}", match.MatchId, match.Region);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error upserting match {MatchId} for region {Region}", match.MatchId, match.Region);
      throw;
    }
  }

  public async Task BatchUpsertMatchesAsync(List<MatchDocument> matches, string region)
  {
    if (!matches.Any())
    {
      _logger.LogDebug("No matches to batch upsert");
      return;
    }

    var container = await GetContainerAsync("matches");
    const int batchSize = 50;
    var batches = matches
        .Select((item, index) => new { item, index })
        .GroupBy(x => x.index / batchSize)
        .Select(g => g.Select(x => x.item).ToList())
        .ToList();

    _logger.LogInformation("Batch upserting {TotalCount} matches in {BatchCount} batches for region {Region}",
        matches.Count, batches.Count, region);

    var totalProcessed = 0;
    var totalErrors = 0;

    foreach (var batch in batches)
    {
      var transactionalBatch = container.CreateTransactionalBatch(new PartitionKey(region));

      foreach (var match in batch)
      {
        transactionalBatch.UpsertItem(match);
      }

      var batchResponse = await transactionalBatch.ExecuteAsync();

      if (batchResponse.IsSuccessStatusCode)
      {
        totalProcessed += batch.Count;
        _logger.LogDebug("Successfully batch upserted {Count} matches for region {Region}",
            batch.Count, region);
      }
      else
      {
        _logger.LogError("Batch upsert failed with status code: {StatusCode} for region {Region}",
            batchResponse.StatusCode, region);
        totalErrors += batch.Count;
      }
    }

    _logger.LogInformation("Batch upsert completed for region {Region}. Processed: {Processed}, Errors: {Errors}",
        region, totalProcessed, totalErrors);

    if (totalErrors > 0)
    {
      throw new Exception($"Batch upsert completed with {totalErrors} errors for region {region}");
    }
  }
  public async Task<List<MatchDocument>> GetUnprocessedMatchesAsync(string region, int maxCount = 120, DateTime? maxCreatedTime = null)
  {
    var container = await GetContainerAsync("matches");

    var queryText = "SELECT * FROM c WHERE c.region = @region AND c.processed = false";
    var queryDefinition = new QueryDefinition(queryText)
      .WithParameter("@region", region);

    if (maxCreatedTime.HasValue)
    {
      queryText += " AND c.created_at <= @maxCreatedTime";
      queryDefinition = new QueryDefinition(queryText)
        .WithParameter("@region", region)
        .WithParameter("@maxCreatedTime", maxCreatedTime.Value);
    }

    var matches = new List<MatchDocument>();
    var iterator = container.GetItemQueryIterator<MatchDocument>(
      queryDefinition,
      requestOptions: new QueryRequestOptions
      {
        MaxItemCount = maxCount,
        PartitionKey = new PartitionKey(region)
      });

    while (iterator.HasMoreResults && matches.Count < maxCount)
    {
      var response = await iterator.ReadNextAsync();
      matches.AddRange(response);
    }

    _logger.LogInformation("Retrieved {Count} unprocessed matches for region {Region} (maxCreatedTime: {MaxCreatedTime})",
        matches.Count, region, maxCreatedTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "none");

    return matches;
  }
}
