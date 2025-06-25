
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

public class MatchDataCollectionActivities
{
  private readonly IRiotApiService _riotApiService;
  private readonly ICosmosDbService _cosmosDbService;
  private readonly ILogger<MatchDataCollectionActivities> _logger;

  public MatchDataCollectionActivities(
      IRiotApiService riotApiService,
      ICosmosDbService cosmosDbService,
      ILogger<MatchDataCollectionActivities> logger)
  {
    _riotApiService = riotApiService;
    _cosmosDbService = cosmosDbService;
    _logger = logger;
  }

  [Function("GetMatchCollectionTotalCountActivity")]
  public async Task<Dictionary<string, int>> GetMatchCollectionTotalCountActivity([ActivityTrigger] string input)
  {
    _logger.LogInformation("Getting total match count per region");

    await _cosmosDbService.InitializeAsync();

    var totalCountPerRegion = new Dictionary<string, int>();

    foreach (var matchRegion in Constants.MatchRegions)
    {
      // Get the regions that map to this match region
      var regions = Constants.RegionToMatchRegion
          .Where(kvp => kvp.Value == matchRegion)
          .Select(kvp => kvp.Key)
          .ToList();

      var totalCount = 0;
      foreach (var region in regions)
      {
        var unprocessedMatches = await _cosmosDbService.GetUnprocessedMatchesAsync(region, int.MaxValue);
        totalCount += unprocessedMatches.Count;
      }

      totalCountPerRegion[matchRegion] = totalCount;
      _logger.LogInformation("Match region {MatchRegion} has {Count} unprocessed matches", matchRegion, totalCount);
    }

    return totalCountPerRegion;
  }

  [Function("CollectMatchDataActivity")]
  public async Task<MatchDataProcessingState> CollectMatchDataActivity([ActivityTrigger] MatchDataProcessingState processingState)
  {
    _logger.LogInformation("Processing match data for region {MatchRegion}", processingState.MatchRegion);

    var activityStartTime = DateTime.UtcNow;
    await _cosmosDbService.InitializeAsync();

    // Get the regions that map to this match region
    var regions = Constants.RegionToMatchRegion
        .Where(kvp => kvp.Value == processingState.MatchRegion)
        .Select(kvp => kvp.Key)
        .ToList();

    var processedMatches = new List<MatchDocument>();

    // Phase 1: Collect all match data from Riot API
    foreach (var region in regions)
    {
      if (!Utils.ShouldContinueActivity(activityStartTime))
      {
        _logger.LogInformation("Activity time limit reached for match region {MatchRegion}", processingState.MatchRegion);
        break;
      }

      // Get unprocessed matches for this region
      var matches = await _cosmosDbService.GetUnprocessedMatchesAsync(region, Utils.ActivityConstants.MaxMatchesToFetch, processingState.MaxCreatedOn);
      var isRateLimited = false;
      _logger.LogInformation("Retrieved {Count} unprocessed matches for region {Region}", matches.Count, region);

      foreach (var match in matches)
      {
        var (matchData, rateLimitInfo) = await _riotApiService.GetMatchAsync(processingState.MatchRegion, match.MatchId);

        if (rateLimitInfo.IsRateLimited)
        {
          _logger.LogWarning("Rate limit wait time {WaitTime}s exceeds maximum for match region {MatchRegion}, stopping API calls", rateLimitInfo.RetryAfterSeconds, processingState.MatchRegion);
          processingState.EndOfRateLimit = DateTime.UtcNow.AddSeconds(rateLimitInfo.RetryAfterSeconds);
          isRateLimited = true;
          break;
        }

        match.MatchData = matchData!;
        match.Processed = true;
        processedMatches.Add(match);

        _logger.LogDebug("Collected match data for {MatchId} from region {Region}", match.MatchId, region);
      }

      if (isRateLimited)
      {
        break;
      }
    }

    // Phase 2: Batch save all processed matches to database
    if (processedMatches.Any())
    {
      _logger.LogInformation("Batch saving {Count} processed matches to database", processedMatches.Count);

      var matchesByRegion = processedMatches.GroupBy(m => m.Region);
      foreach (var regionGroup in matchesByRegion)
      {
        var region = regionGroup.Key;
        var matches = regionGroup.ToList();

        _logger.LogInformation("Batch upserting {Count} matches for region {Region}", matches.Count, region);
        var result = await _cosmosDbService.BatchUpsertMatchesAsync(matches, region);

        if (result.HasErrors)
        {
          _logger.LogWarning("Batch upsert completed with {ErrorCount} errors out of {TotalCount} matches for region {Region}. Processed: {ProcessedCount}",
              result.TotalErrors, matches.Count, region, result.TotalProcessed);
        }
        else
        {
          _logger.LogInformation("Successfully batch upserted all {Count} matches for region {Region}",
              result.TotalProcessed, region);
        }
      }
    }
    processingState.TotalProcessed += processedMatches.Count;

    _logger.LogInformation("Completed match data processing for region {MatchRegion}. Processed: {Count}",
        processingState.MatchRegion, processedMatches.Count);

    return processingState;
  }
}