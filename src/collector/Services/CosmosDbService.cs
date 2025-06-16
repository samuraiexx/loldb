using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using collector.Models;
using Newtonsoft.Json;

namespace collector.Services;

public interface ICosmosDbService
{
  Task<PlayerStatsDocument?> GetPlayerStatsAsync(string puuid, string queueType);
  Task UpsertPlayerStatsAsync(PlayerStatsDocument playerStats, string queueType);
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

  public async Task<PlayerStatsDocument?> GetPlayerStatsAsync(string puuid, string queueType)
  {
    try
    {
      var container = await GetContainerAsync(queueType);
      var response = await container.ReadItemAsync<PlayerStatsDocument>(puuid, new PartitionKey(puuid));
      return response.Resource;
    }
    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
      return null;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting player stats for {Puuid} in {QueueType}", puuid, queueType);
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
}
