
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

public class MatchDataCollectionActivities
{
  private readonly IRiotApiService _riotApiService;
  private readonly ICosmosDbService _cosmosDbService;
  private readonly IBlobStorageService _blobStorageService;
  private readonly ILogger<MatchDataCollectionActivities> _logger;

  public MatchDataCollectionActivities(
      IRiotApiService riotApiService,
      ICosmosDbService cosmosDbService,
      IBlobStorageService blobStorageService,
      ILogger<MatchDataCollectionActivities> logger)
  {
    _riotApiService = riotApiService;
    _cosmosDbService = cosmosDbService;
    _blobStorageService = blobStorageService;
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
    var rateLimitEndTime = DateTime.MinValue;

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
      _logger.LogInformation("Retrieved {Count} unprocessed matches for region {Region}", matches.Count, region);

      foreach (var match in matches)
      {
        if (!Utils.ShouldContinueActivity(activityStartTime))
        {
          _logger.LogInformation("Activity time limit reached for match region {MatchRegion}", processingState.MatchRegion);
          break;
        }

        var (matchData, rateLimitInfo) = await _riotApiService.GetMatchAsync(processingState.MatchRegion, match.MatchId);

        if (rateLimitInfo.IsRateLimited)
        {
          var waitTime = TimeSpan.FromSeconds(rateLimitInfo.RetryAfterSeconds);
          if (Utils.ShouldStopForRateLimit(waitTime))
          {
            _logger.LogWarning("Rate limit wait time {WaitTime} exceeds maximum for match region {MatchRegion}, stopping API calls",
                waitTime, processingState.MatchRegion);
            rateLimitEndTime = DateTime.UtcNow.Add(waitTime);
            break;
          }

          _logger.LogInformation("Rate limited for match region {MatchRegion}, waiting {WaitTime}",
              processingState.MatchRegion, waitTime);
          await Task.Delay(waitTime);
          continue;
        }

        if (matchData != null)
        {
          match.MatchData = matchData;
          match.Processed = true;
          processedMatches.Add(match);

          _logger.LogDebug("Collected match data for {MatchId} from region {Region}", match.MatchId, region);
        }
        else
        {
          _logger.LogWarning("Failed to retrieve match data for {MatchId}", match.MatchId);
        }
      }

      if (rateLimitEndTime > DateTime.MinValue)
        break;
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

        // Save in smaller batches to avoid timeout
        for (int i = 0; i < matches.Count; i += Utils.ActivityConstants.MatchBatchSize)
        {
          var batch = matches.Skip(i).Take(Utils.ActivityConstants.MatchBatchSize).ToList();
          foreach (var match in batch)
          {
            await _cosmosDbService.UpsertMatchAsync(match);
          }

          _logger.LogDebug("Saved batch of {BatchCount} matches for region {Region}", batch.Count, region);
        }
      }
    }

    processingState.TotalProcessed += processedMatches.Count;
    processingState.EndOfRateLimit = rateLimitEndTime;

    _logger.LogInformation("Completed match data processing for region {MatchRegion}. Processed: {Count}",
        processingState.MatchRegion, processedMatches.Count);

    return processingState;
  }
}