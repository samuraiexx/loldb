
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

public class PlayerMatchCollectionActivities
{
  private readonly IRiotApiService _riotApiService;
  private readonly ICosmosDbService _cosmosDbService;
  private readonly ILogger<PlayerMatchCollectionActivities> _logger;

  public PlayerMatchCollectionActivities(
      IRiotApiService riotApiService,
      ICosmosDbService cosmosDbService,
      ILogger<PlayerMatchCollectionActivities> logger)
  {
    _riotApiService = riotApiService;
    _cosmosDbService = cosmosDbService;
    _logger = logger;
  }

  [Function("CollectMatchesForDomainActivity")]
  public async Task<PlayerMatchProcessingState> CollectMatchesForDomainActivity([ActivityTrigger] PlayerMatchProcessingState processingState)
  {
    var matchRegion = processingState.ProcessingScope.First().MatchRegion;
    _logger.LogInformation("Processing match collection for match region {MatchRegion} with {UnitCount} units, max {MaxMatches} matches per unit",
        matchRegion, processingState.ProcessingScope.Count, processingState.MaxMatchesPerUnit);
    var activityStartTime = DateTime.UtcNow;

    var initResult = await _cosmosDbService.InitializeAsync();
    if (!initResult)
    {
      _logger.LogError("Failed to initialize Cosmos DB service for match region {MatchRegion}", matchRegion);
      throw new InvalidOperationException("Cosmos DB initialization failed. Check connection configuration and credentials.");
    }

    var allMatchDocuments = new List<MatchDocument>();
    var processedUnitsCount = 0;
    var rateLimitEndTime = DateTime.MinValue;

    // Phase 1: Collect all match IDs from Riot API
    foreach (var unit in processingState.ProcessingScope.Skip(processingState.TotalProcessed))
    {
      if (!Utils.ShouldContinueActivity(activityStartTime))
      {
        _logger.LogInformation("Activity time limit reached for match region {MatchRegion}", matchRegion);
        break;
      }
      _logger.LogInformation("Processing unit: {Region} {QueueType} {Tier} {Division}",
          unit.Region, unit.QueueType, unit.Tier, unit.Division);      // Get ranked PUUIDs for this unit

      var puuids = await _cosmosDbService.GetRankedPuuidsAsync(unit.QueueType, unit.Tier, unit.Division, unit.Region);
      _logger.LogInformation("Found {Count} PUUIDs for {Region} {QueueType} {Tier} {Division}",
          puuids.Count, unit.Region, unit.QueueType, unit.Tier, unit.Division);

      var unitMatchDocuments = new List<MatchDocument>();
      var totalMatches = 0;
      var matchesCollectedForUnit = 0;

      foreach (var puuid in puuids)
      {
        if (!Utils.ShouldContinueActivity(activityStartTime))
        {
          _logger.LogInformation("Activity time limit reached for match region {MatchRegion}", matchRegion);
          break;
        }

        // Collect exactly 100 matches (one page) per player, no date filtering
        var (matchIds, rateLimitInfo) = await _riotApiService.GetMatchIdsByPuuidAsync(
            matchRegion, puuid, null, null, "ranked", 0, 100);

        if (rateLimitInfo.IsRateLimited)
        {
          var waitTime = TimeSpan.FromSeconds(rateLimitInfo.RetryAfterSeconds);
          _logger.LogWarning("Rate limit wait time {WaitTime} exceeds maximum for match region {MatchRegion}, stopping API calls",
              waitTime, matchRegion);
          rateLimitEndTime = DateTime.UtcNow.Add(waitTime);
          break;
        }

        if (matchIds.Count == 0)
        {
          _logger.LogDebug("No matches found for PUUID {Puuid} in match region {MatchRegion}", puuid, matchRegion);
          continue;
        }

        // Create match documents with basic info
        foreach (var matchId in matchIds)
        {
          // Extract region from match ID (e.g., "NA1_4567890123" -> "NA1")
          var region = matchId.Split('_')[0];

          var matchDoc = new MatchDocument
          {
            Id = matchId,
            MatchId = matchId,
            Region = region, // Use the region extracted from match ID
            Processed = false,
            CreatedAt = DateTime.UtcNow
          };

          unitMatchDocuments.Add(matchDoc);
          matchesCollectedForUnit++;
        }

        totalMatches += matchIds.Count;

        _logger.LogDebug("Collected {Count} match IDs for PUUID {Puuid} in match region {MatchRegion} (unit total: {UnitTotal})",
            matchIds.Count, puuid, matchRegion, matchesCollectedForUnit);
        // Break if we've reached the unit limit or hit rate limits
        if (matchesCollectedForUnit >= processingState.MaxMatchesPerUnit || rateLimitEndTime > DateTime.MinValue)
        {
          break;
        }
      }

      // Add this unit's matches to the overall collection
      allMatchDocuments.AddRange(unitMatchDocuments);
      processedUnitsCount++;
      _logger.LogInformation("Completed unit processing: {Region} {QueueType} {Tier} {Division}. Matches collected: {CollectedMatches}/{MaxMatches}",
          unit.Region, unit.QueueType, unit.Tier, unit.Division, matchesCollectedForUnit, processingState.MaxMatchesPerUnit);

      if (rateLimitEndTime > DateTime.MinValue)
      {
        break;
      }
    }

    // Phase 2: Batch save all collected matches to database
    if (allMatchDocuments.Any())
    {
      _logger.LogInformation("Batch saving {Count} total matches to database", allMatchDocuments.Count);

      var matchesByRegion = allMatchDocuments.GroupBy(m => m.Region);
      foreach (var regionGroup in matchesByRegion)
      {
        var region = regionGroup.Key;
        var matches = regionGroup.ToList();

        _logger.LogInformation("Batch upserting {Count} matches for region {Region}", matches.Count, region);
        await _cosmosDbService.BatchUpsertMatchesAsync(matches, region);
      }
    }

    processingState.TotalProcessed += processedUnitsCount;
    processingState.EndOfRateLimit = rateLimitEndTime;

    _logger.LogInformation("Completed match collection for match region {MatchRegion}. Units processed: {Count}, Total matches collected: {MatchCount}",
        matchRegion, processedUnitsCount, allMatchDocuments.Count);

    return processingState;
  }
}