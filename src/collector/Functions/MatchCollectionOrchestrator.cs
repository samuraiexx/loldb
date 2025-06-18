using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using collector.Models;
using collector.Services;

namespace collector.Functions;

public class MatchCollectionOrchestrator
{
  private readonly IBlobStorageService _blobStorageService;
  private readonly ICosmosDbService _cosmosDbService;
  private readonly ILogger<MatchCollectionOrchestrator> _logger;

  public MatchCollectionOrchestrator(
      IBlobStorageService blobStorageService,
      ICosmosDbService cosmosDbService,
      ILogger<MatchCollectionOrchestrator> logger)
  {
    _blobStorageService = blobStorageService;
    _cosmosDbService = cosmosDbService;
    _logger = logger;
  }

  [Function("MatchCollectionOrchestrator")]
  public async Task<string> RunOrchestratorAsync([OrchestrationTrigger] TaskOrchestrationContext context)
  {
    var logger = context.CreateReplaySafeLogger("MatchCollectionOrchestrator");
    logger.LogInformation("Starting Match Collection Orchestrator");

    try
    {      // Get configuration and calculate time range
      var config = await context.CallActivityAsync<MatchCollectionConfig>("GetMatchCollectionConfigActivity", string.Empty);
      var startTime = config.StartTime;
      var endTime = DateTime.UtcNow;

      logger.LogInformation("Collecting matches from {StartTime} to {EndTime}", startTime, endTime);

      // Get all ranked PUUIDs
      var puuids = await context.CallActivityAsync<List<string>>("GetRankedPuuidsActivity", "RANKED_SOLO_5x5");
      logger.LogInformation("Found {Count} ranked PUUIDs to process", puuids.Count);

      if (puuids.Count == 0)
      {
        logger.LogWarning("No ranked PUUIDs found, ending orchestration");
        return "No ranked PUUIDs found";
      }

      // Group PUUIDs by domain for parallel processing
      var puuidsByDomain = GroupPuuidsByDomain(puuids);
      logger.LogInformation("Grouped PUUIDs across {DomainCount} domains", puuidsByDomain.Count);

      // Initialize collection states
      var collectionStates = puuidsByDomain.Select(kvp => new MatchCollectionState
      {
        Domain = kvp.Key,
        StartTime = startTime,
        EndTime = endTime,
        Puuids = kvp.Value,
        IsCompleted = false,
        LastProcessed = DateTime.UtcNow,
        TotalMatchesCollected = 0
      }).ToList();

      // Phase 1: Collect match IDs
      logger.LogInformation("Phase 1: Starting match ID collection for {DomainCount} domains", collectionStates.Count);

      var matchCollectionTasks = collectionStates.Select(state =>
          context.CallActivityAsync<MatchCollectionState>("CollectMatchesForDomainActivity", state)
      ).ToArray();

      var collectionResults = await Task.WhenAll(matchCollectionTasks);
      var totalMatchesCollected = collectionResults.Sum(r => r.TotalMatchesCollected);

      logger.LogInformation("Phase 1 completed. Total matches collected: {TotalMatches}", totalMatchesCollected);

      // Phase 2: Process match details
      logger.LogInformation("Phase 2: Starting match details processing");

      var detailsProcessingTasks = Constants.Domains.Select(domain =>
          context.CallActivityAsync<int>("ProcessMatchDetailsActivity", domain)
      ).ToArray();

      var detailsResults = await Task.WhenAll(detailsProcessingTasks);
      var totalMatchesProcessed = detailsResults.Sum();

      logger.LogInformation("Phase 2 completed. Total matches processed: {TotalProcessed}", totalMatchesProcessed);

      // Update start time configuration
      var newConfig = new MatchCollectionConfig { StartTime = endTime };
      await context.CallActivityAsync("SaveMatchCollectionConfigActivity", newConfig);

      logger.LogInformation("Updated start time configuration to {NewStartTime}", endTime);

      var result = $"Match collection completed. Collected: {totalMatchesCollected}, Processed: {totalMatchesProcessed}";
      logger.LogInformation(result);

      return result;
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error in match collection orchestration");
      throw;
    }
  }

  private Dictionary<string, List<string>> GroupPuuidsByDomain(List<string> puuids)
  {
    // For simplicity, distribute PUUIDs evenly across domains
    // In a real implementation, you might want to group by the player's primary region
    var domains = Constants.Domains;
    var puuidsByDomain = new Dictionary<string, List<string>>();

    foreach (var domain in domains)
    {
      puuidsByDomain[domain] = new List<string>();
    }

    for (int i = 0; i < puuids.Count; i++)
    {
      var domain = domains[i % domains.Length];
      puuidsByDomain[domain].Add(puuids[i]);
    }

    return puuidsByDomain;
  }
  [Function("GetMatchCollectionConfigActivity")]
  public async Task<MatchCollectionConfig> GetMatchCollectionConfigActivity([ActivityTrigger] string input)
  {
    return await _blobStorageService.GetMatchCollectionConfigAsync();
  }

  [Function("SaveMatchCollectionConfigActivity")]
  public async Task SaveMatchCollectionConfigActivity([ActivityTrigger] MatchCollectionConfig config)
  {
    await _blobStorageService.SaveMatchCollectionConfigAsync(config);
  }

  [Function("GetRankedPuuidsActivity")]
  public async Task<List<string>> GetRankedPuuidsActivity([ActivityTrigger] string queueType)
  {
    var minimumTier = Environment.GetEnvironmentVariable("MINIMUM_TIER") ?? "IRON";
    var minimumDivision = Environment.GetEnvironmentVariable("MINIMUM_DIVISION") ?? "V";

    _logger.LogInformation("Getting ranked PUUIDs for {QueueType} with minimum rank {MinTier} {MinDivision}",
        queueType, minimumTier, minimumDivision);

    return await _cosmosDbService.GetRankedPuuidsAsync(queueType, minimumTier, minimumDivision);
  }
}