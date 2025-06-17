using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using collector.Models;
using Newtonsoft.Json;

namespace collector.Services;

public interface ICosmosDbService
{
  Task<PlayerStatsDocument?> GetPlayerStatsAsync(string puuid, string queueType, string region);
  Task UpsertPlayerStatsAsync(PlayerStatsDocument playerStats, string queueType);
  Task BatchUpsertPlayerStatsAsync(List<PlayerStatsDocument> playerStatsList, string queueType, string region);
  Task<bool> InitializeAsync();
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

      // Create database if it doesn't exist
      var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName);
      _database = databaseResponse.Database;

      _logger.LogInformation("Database '{DatabaseName}' ready", _databaseName);
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to initialize Cosmos DB service");
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
      throw new InvalidOperationException("Database not initialized. Call InitializeAsync first.");
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
}
