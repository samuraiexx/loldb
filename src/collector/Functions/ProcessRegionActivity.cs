using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using collector.Models;
using collector.Services;

namespace collector.Functions;

public class ProcessRegionActivity
{
  private readonly IRiotApiService _riotApiService;
  private readonly ICosmosDbService _cosmosDbService;
  private readonly ILogger<ProcessRegionActivity> _logger;

  public ProcessRegionActivity(
      IRiotApiService riotApiService,
      ICosmosDbService cosmosDbService,
      ILogger<ProcessRegionActivity> logger)
  {
    _riotApiService = riotApiService;
    _cosmosDbService = cosmosDbService;
    _logger = logger;
  }

  [Function("ProcessRegionActivity")]
  public async Task<List<ProcessingState>> RunAsync(
      [ActivityTrigger] List<ProcessingState> regionStates)
  {
    var region = regionStates.First().Region;
    _logger.LogInformation("Processing region {Region} with {StateCount} states", region, regionStates.Count);

    var updatedStates = new List<ProcessingState>();
    var startTime = DateTime.UtcNow;
    var maxProcessingTime = TimeSpan.FromMinutes(30);

    try
    {
      // Ensure Cosmos DB is initialized
      await _cosmosDbService.InitializeAsync();

      foreach (var state in regionStates.Where(s => !s.IsCompleted))
      {
        // Check if we've exceeded our time limit before starting a new state
        if (DateTime.UtcNow - startTime >= maxProcessingTime)
        {
          _logger.LogInformation("30-minute time limit reached, stopping region processing for {Region}", region);
          break;
        }

        _logger.LogInformation("Processing {Region} - {QueueType} {Tier} {Division} starting from page {Page}",
            state.Region, state.QueueType, state.Tier, state.Division, state.Page);

        var currentState = new ProcessingState
        {
          Region = state.Region,
          QueueType = state.QueueType,
          Tier = state.Tier,
          Division = state.Division,
          Page = state.Page,
          TotalProcessed = state.TotalProcessed,
          LastProcessed = state.LastProcessed
        };

        var currentPage = state.Page;
        var stateEntriesProcessed = 0;        // Continue processing pages until we hit time limit or no more data

        while (DateTime.UtcNow - startTime < maxProcessingTime)
        {
          var (entries, rateLimitInfo) = await _riotApiService.GetLeagueEntriesAsync(
              state.Region, state.QueueType, state.Tier, state.Division, currentPage);

          if (rateLimitInfo.IsRateLimited)
          {
            _logger.LogWarning("Rate limit hit for {Region}. Retry after: {RetryAfter}s",
                state.Region, rateLimitInfo.RetryAfterSeconds);

            if (rateLimitInfo.RetryAfterSeconds <= 10)
            {
              // Short rate limit - wait inline
              _logger.LogInformation("Waiting {Seconds}s for short rate limit", rateLimitInfo.RetryAfterSeconds);
              await Task.Delay(TimeSpan.FromSeconds(rateLimitInfo.RetryAfterSeconds + 1));
              continue;
            }
            else
            {
              // Long rate limit - stop processing this region
              _logger.LogWarning("Long rate limit encountered, stopping region processing");
              break;
            }
          }

          if (entries.Count == 0)
          {
            // No more entries, this state is completed
            _logger.LogInformation("No more entries for {Region} - {QueueType} {Tier} {Division}. Completed.",
                state.Region, state.QueueType, state.Tier, state.Division);
            currentState.IsCompleted = true;
            break;
          }

          // Process and save entries
          await ProcessAndSaveEntries(entries, state.QueueType, state.Region);

          stateEntriesProcessed += entries.Count;
          currentPage++;

          _logger.LogDebug("Processed page {Page} for {Region} - {QueueType} {Tier} {Division}: {Count} entries",
              currentPage - 1, state.Region, state.QueueType, state.Tier, state.Division, entries.Count);

          // Log rate limit status
          _logger.LogDebug("Rate limit status for {Region}: {Current2Min}/{Max2Min} (2min), {Current1Sec}/{Max1Sec} (1sec)",
              state.Region, rateLimitInfo.CurrentRequestsPer2Minutes, rateLimitInfo.RequestsPer2Minutes,
              rateLimitInfo.CurrentRequestsPerSecond, rateLimitInfo.RequestsPerSecond);          // Check if we're approaching the 2-minute rate limit
          if (rateLimitInfo.CurrentRequestsPer2Minutes >= rateLimitInfo.RequestsPer2Minutes * 0.9)
          {
            _logger.LogInformation("Approaching 2-minute rate limit for {Region}, stopping processing", state.Region);
            break;
          }

          // Check if we've exceeded our time limit
          if (DateTime.UtcNow - startTime >= maxProcessingTime)
          {
            _logger.LogInformation("30-minute time limit reached for {Region}, stopping processing", state.Region);
            break;
          }
        }

        currentState.Page = currentPage;
        currentState.TotalProcessed += stateEntriesProcessed;
        currentState.LastProcessed = DateTime.UtcNow;

        updatedStates.Add(currentState);

        _logger.LogInformation("Completed processing state for {Region} - {QueueType} {Tier} {Division}. " +
            "Next page: {Page}, Total processed: {Total}, Completed: {IsCompleted}",
            currentState.Region, currentState.QueueType, currentState.Tier, currentState.Division,
            currentState.Page, currentState.TotalProcessed, currentState.IsCompleted);
      }

      // Add completed states unchanged
      updatedStates.AddRange(regionStates.Where(s => s.IsCompleted));
      var processingTime = DateTime.UtcNow - startTime;
      _logger.LogInformation("Completed processing region {Region}. " +
          "Processing time: {ProcessingTime}, States processed: {StatesProcessed}",
          region, processingTime, updatedStates.Count(s => !s.IsCompleted));

      return updatedStates;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing region {Region}", region);

      // Return original states on error to prevent losing progress
      return regionStates;
    }
  }

  private async Task ProcessAndSaveEntries(List<LeagueEntryDTO> entries, string queueType, string region)
  {
    var processed = 0;
    var errors = 0;

    foreach (var entry in entries)
    {
      try
      {
        // Get existing player stats or create new
        var existingStats = await _cosmosDbService.GetPlayerStatsAsync(entry.Puuid, queueType, region);

        var playerStats = existingStats ?? new PlayerStatsDocument
        {
          Id = entry.Puuid,
          Puuid = entry.Puuid,
          SummonerId = entry.SummonerId,
          LeagueId = entry.LeagueId,
          Region = region,
          CreatedAt = DateTime.UtcNow,
          Snapshots = new List<PlayerSnapshot>()
        };

        // Create new snapshot
        var snapshot = new PlayerSnapshot
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
        };

        // Add snapshot to player stats
        playerStats.Snapshots.Add(snapshot);
        playerStats.LastUpdated = DateTime.UtcNow;

        // Save to Cosmos DB
        await _cosmosDbService.UpsertPlayerStatsAsync(playerStats, queueType);

        processed++;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error processing entry for player {Puuid} in {QueueType}",
            entry.Puuid, queueType);
        errors++;
      }
    }

    _logger.LogDebug("Processed {Processed} entries, {Errors} errors for {QueueType} in {Region}",
        processed, errors, queueType, region);
  }
}
