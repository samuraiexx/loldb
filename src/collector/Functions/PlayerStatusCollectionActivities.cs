using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

public class PlayerStatusCollectionActivities
{
  private readonly RiotApiService _riotApiService;
  private readonly AzureDataLakeService _dataService;
  private readonly ILogger<PlayerStatusCollectionActivities> _logger;

  public PlayerStatusCollectionActivities(
      RiotApiService riotApiService,
      AzureDataLakeService dataService,
      ILogger<PlayerStatusCollectionActivities> logger)
  {
    _riotApiService = riotApiService;
    _dataService = dataService;
    _logger = logger;
  }
  [Function("PlayerStatusCollectionActivity")]
  public async Task<PlayerStatusProcessingState> PlayerStatusCollectionActivity([ActivityTrigger] PlayerStatusProcessingState processingState)
  {
    var region = processingState.ProcessingScope.First().Region;
    _logger.LogInformation("Processing player status collection for region {Region} with {UnitCount} units",
        region, processingState.ProcessingScope.Count);

    var activityStartTime = DateTime.UtcNow;
    await _dataService.InitializeAsync();

    var allPlayerEntries = new List<(LeagueEntryDTO entry, string queueType, string region)>();
    var processedUnitsCount = 0;
    var rateLimitEndTime = DateTime.MinValue;

    // Phase 1: Collect all player entries from Riot API
    foreach (var unit in processingState.ProcessingScope.Skip(processingState.TotalProcessed))
    {
      if (!Utils.ShouldContinueActivity(activityStartTime))
      {
        _logger.LogInformation("Activity time limit reached for region {Region}", region);
        break;
      }

      _logger.LogInformation("Processing unit: {Region} {QueueType} {Tier} {Division} starting from page {Page}",
          unit.Region, unit.QueueType, unit.Tier, unit.Division, processingState.LastProcessedPage + 1);

      var currentPage = processingState.LastProcessedPage + 1;
      var unitEntriesCollected = new List<LeagueEntryDTO>();

      // Continue processing pages until we hit time limit or no more data
      while (Utils.ShouldContinueActivity(activityStartTime))
      {
        var (entries, rateLimitInfo) = await _riotApiService.GetLeagueEntriesAsync(
            unit.Region, unit.QueueType, unit.Tier, unit.Division, currentPage);

        if (rateLimitInfo.IsRateLimited)
        {
          var waitTime = TimeSpan.FromSeconds(rateLimitInfo.RetryAfterSeconds);
          if (Utils.ShouldStopForRateLimit(waitTime))
          {
            _logger.LogWarning("Long rate limit encountered, stopping API calls");
            rateLimitEndTime = DateTime.UtcNow.AddSeconds(rateLimitInfo.RetryAfterSeconds);
            break;
          }

          _logger.LogInformation("Rate limited for {Region}, waiting {WaitTime}",
              unit.Region, waitTime);
          await Task.Delay(waitTime);
          continue;
        }

        if (entries.Count == 0)
        {
          // No more entries, move to next unit
          _logger.LogInformation("No more entries for {Region} - {QueueType} {Tier} {Division}. Moving to next unit.",
              unit.Region, unit.QueueType, unit.Tier, unit.Division);
          break;
        }

        // Collect entries for later batch processing
        unitEntriesCollected.AddRange(entries);
        currentPage++;

        _logger.LogDebug("Collected page {Page} for {Region} - {QueueType} {Tier} {Division}: {Count} entries",
            currentPage - 1, unit.Region, unit.QueueType, unit.Tier, unit.Division, entries.Count);

        // Log rate limit status
        _logger.LogDebug("Rate limit status for {Region}: {Current2Min}/{Max2Min} (2min), {Current1Sec}/{Max1Sec} (1sec)",
            unit.Region, rateLimitInfo.CurrentRequestsPer2Minutes, rateLimitInfo.RequestsPer2Minutes,
            rateLimitInfo.CurrentRequestsPerSecond, rateLimitInfo.RequestsPerSecond);
      }

      // Add this unit's entries to the overall collection
      foreach (var entry in unitEntriesCollected)
      {
        allPlayerEntries.Add((entry, unit.QueueType, unit.Region));
      }

      processedUnitsCount++;
      processingState.LastProcessedPage = currentPage - 1;

      _logger.LogInformation("Completed collecting entries for unit {Region} - {QueueType} {Tier} {Division}. " +
          "Last page: {Page}, Unit entries collected: {UnitEntries}",
          unit.Region, unit.QueueType, unit.Tier, unit.Division,
          processingState.LastProcessedPage, unitEntriesCollected.Count);

      if (rateLimitEndTime > DateTime.MinValue)
        break;
    }

    // Phase 2: Batch save all collected entries to database
    if (allPlayerEntries.Any())
    {
      _logger.LogInformation("Batch saving {Count} total player entries to database", allPlayerEntries.Count);

      // Group by queue type and region for efficient batch processing
      var entriesByQueueAndRegion = allPlayerEntries
          .GroupBy(x => new { x.queueType, x.region })
          .ToList();

      foreach (var group in entriesByQueueAndRegion)
      {
        var queueType = group.Key.queueType;
        var regionKey = group.Key.region;
        var entries = group.Select(x => x.entry).ToList();

        _logger.LogInformation("Batch processing {Count} entries for {QueueType} in {Region}",
            entries.Count, queueType, regionKey);

        await ProcessAndSaveEntriesBatch(entries, queueType, regionKey);
      }
    }

    processingState.TotalProcessed += processedUnitsCount;
    processingState.EndOfRateLimit = rateLimitEndTime;

    var processingTime = DateTime.UtcNow - activityStartTime;
    _logger.LogInformation("Completed processing region {Region}. " +
        "Processing time: {ProcessingTime}, Units processed: {UnitsProcessed}, Total entries collected: {TotalEntries}",
        region, processingTime, processedUnitsCount, allPlayerEntries.Count);

    return processingState;
  }

  private async Task ProcessAndSaveEntriesBatch(List<LeagueEntryDTO> entries, string queueType, string region)
  {
    _logger.LogDebug("Processing {Count} entries for batch upsert in {QueueType} for {Region}",
        entries.Count, queueType, region);

    var playerStatsDocuments = new List<PlayerStatsDocument>();
    var processed = 0;
    var errors = 0;

    foreach (var entry in entries)
    {
      // Create new player stats document with single snapshot
      var playerStats = new PlayerStatsDocument
      {
        Id = entry.Puuid,
        Puuid = entry.Puuid,
        SummonerId = entry.SummonerId,
        LeagueId = entry.LeagueId,
        Region = region,
        CreatedAt = DateTime.UtcNow,
        LastUpdated = DateTime.UtcNow,
        Snapshot = new PlayerSnapshot
        {
          Timestamp = DateTime.UtcNow,
          Tier = entry.Tier,
          Rank = entry.Rank,
          LeaguePoints = entry.LeaguePoints,
          Wins = entry.Wins,
          Losses = entry.Losses,
          HotStreak = entry.HotStreak,
          Veteran = entry.Veteran,
          FreshBlood = entry.FreshBlood,
          Inactive = entry.Inactive,
          MiniSeries = entry.MiniSeries
        }
      };

      playerStatsDocuments.Add(playerStats);
      processed++;
    }

    // Batch upsert all documents
    if (playerStatsDocuments.Any())
    {
      await _dataService.BatchUpsertPlayerStatsAsync(playerStatsDocuments, queueType, region);
      _logger.LogDebug("Batch processed {Processed} entries, {Errors} errors for {QueueType} in {Region}",
          processed, errors, queueType, region);
    }
    else
    {
      _logger.LogWarning("No valid player stats documents to batch upsert for {QueueType} in {Region}",
          queueType, region);
    }
  }
}
