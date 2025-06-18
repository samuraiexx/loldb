using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using collector.Models;
using collector.Services;

namespace collector.Functions;

public class MatchCollectionActivities
{
  private readonly IRiotApiService _riotApiService;
  private readonly ICosmosDbService _cosmosDbService;
  private readonly ILogger<MatchCollectionActivities> _logger;

  public MatchCollectionActivities(
      IRiotApiService riotApiService,
      ICosmosDbService cosmosDbService,
      ILogger<MatchCollectionActivities> logger)
  {
    _riotApiService = riotApiService;
    _cosmosDbService = cosmosDbService;
    _logger = logger;
  }

  [Function("CollectMatchesForDomainActivity")]
  public async Task<MatchCollectionState> CollectMatchesForDomainAsync(
      [ActivityTrigger] MatchCollectionState input)
  {
    _logger.LogInformation("Starting match collection for domain {Domain} with {PuuidCount} PUUIDs",
        input.Domain, input.Puuids.Count);

    var activityStartTime = DateTime.UtcNow;
    var maxActivityTime = TimeSpan.FromMinutes(30);
    var maxWaitTime = TimeSpan.FromMinutes(1);

    var startTimeEpoch = ((DateTimeOffset)input.StartTime).ToUnixTimeSeconds();
    var endTimeEpoch = ((DateTimeOffset)input.EndTime).ToUnixTimeSeconds();

    var matchDocuments = new List<MatchDocument>();
    var totalMatches = 0;

    try
    {
      foreach (var puuid in input.Puuids)
      {
        // Check if we should continue
        if (DateTime.UtcNow - activityStartTime > maxActivityTime)
        {
          _logger.LogInformation("Activity time limit reached for domain {Domain}", input.Domain);
          break;
        }

        var start = 0;
        const int count = 100; // Max allowed
        var hasMoreMatches = true;

        while (hasMoreMatches && DateTime.UtcNow - activityStartTime < maxActivityTime)
        {
          var (matchIds, rateLimitInfo) = await _riotApiService.GetMatchIdsByPuuidAsync(
              input.Domain, puuid, startTimeEpoch, endTimeEpoch, "ranked", start, count);

          if (rateLimitInfo.IsRateLimited)
          {
            var waitTime = TimeSpan.FromSeconds(rateLimitInfo.RetryAfterSeconds);
            if (waitTime > maxWaitTime)
            {
              _logger.LogWarning("Rate limit wait time {WaitTime} exceeds maximum for domain {Domain}, stopping",
                  waitTime, input.Domain);
              input.IsCompleted = true;
              break;
            }

            _logger.LogInformation("Rate limited for domain {Domain}, waiting {WaitTime}",
                input.Domain, waitTime);
            await Task.Delay(waitTime);
            continue;
          }

          if (matchIds.Count == 0)
          {
            _logger.LogDebug("No more matches for PUUID {Puuid} in domain {Domain}", puuid, input.Domain);
            hasMoreMatches = false;
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
              Region = region,
              Processed = false,
              CreatedAt = DateTime.UtcNow
            };

            matchDocuments.Add(matchDoc);
          }

          totalMatches += matchIds.Count;
          start += matchIds.Count;

          // If we got fewer matches than requested, we've reached the end
          if (matchIds.Count < count)
          {
            hasMoreMatches = false;
          }

          _logger.LogDebug("Collected {Count} match IDs for PUUID {Puuid} in domain {Domain} (total: {Total})",
              matchIds.Count, puuid, input.Domain, totalMatches);
        }

        if (input.IsCompleted)
          break;
      }

      // Batch upsert matches by region
      var matchesByRegion = matchDocuments.GroupBy(m => m.Region);
      foreach (var regionGroup in matchesByRegion)
      {
        var region = regionGroup.Key;
        var matches = regionGroup.ToList();

        _logger.LogInformation("Batch upserting {Count} matches for region {Region}",
            matches.Count, region);

        await _cosmosDbService.BatchUpsertMatchesAsync(matches, region);
      }

      input.TotalMatchesCollected = totalMatches;
      input.LastProcessed = DateTime.UtcNow;

      _logger.LogInformation("Completed match collection for domain {Domain}. Total matches: {Total}",
          input.Domain, totalMatches);

      return input;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error collecting matches for domain {Domain}", input.Domain);
      throw;
    }
  }

  [Function("ProcessMatchDetailsActivity")]
  public async Task<int> ProcessMatchDetailsAsync([ActivityTrigger] string domain)
  {
    _logger.LogInformation("Starting match details processing for domain {Domain}", domain);

    var activityStartTime = DateTime.UtcNow;
    var maxActivityTime = TimeSpan.FromMinutes(30);
    var maxWaitTime = TimeSpan.FromMinutes(1);

    var processedCount = 0;
    var regionsInDomain = GetRegionsForDomain(domain);

    try
    {
      foreach (var region in regionsInDomain)
      {
        if (DateTime.UtcNow - activityStartTime > maxActivityTime)
        {
          _logger.LogInformation("Activity time limit reached for domain {Domain}", domain);
          break;
        }        // Get unprocessed matches for this region
        var matches = await _cosmosDbService.GetUnprocessedMatchesAsync(region);

        foreach (var match in matches)
        {
          if (DateTime.UtcNow - activityStartTime > maxActivityTime)
          {
            _logger.LogInformation("Activity time limit reached for domain {Domain}", domain);
            break;
          }

          var (matchData, rateLimitInfo) = await _riotApiService.GetMatchAsync(domain, match.MatchId);

          if (rateLimitInfo.IsRateLimited)
          {
            var waitTime = TimeSpan.FromSeconds(rateLimitInfo.RetryAfterSeconds);
            if (waitTime > maxWaitTime)
            {
              _logger.LogWarning("Rate limit wait time {WaitTime} exceeds maximum for domain {Domain}, stopping",
                  waitTime, domain);
              break;
            }

            _logger.LogInformation("Rate limited for domain {Domain}, waiting {WaitTime}",
                domain, waitTime);
            await Task.Delay(waitTime);
            continue;
          }

          if (matchData != null)
          {
            match.MatchData = matchData;
            match.Processed = true;
            await _cosmosDbService.UpsertMatchAsync(match);
            processedCount++;

            _logger.LogDebug("Processed match {MatchId} for domain {Domain}", match.MatchId, domain);
          }
        }
      }

      _logger.LogInformation("Completed match details processing for domain {Domain}. Processed: {Count}",
          domain, processedCount);

      return processedCount;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing match details for domain {Domain}", domain);
      throw;
    }
  }
  private List<string> GetRegionsForDomain(string domain)
  {
    return Constants.RegionToDomain
        .Where(kvp => kvp.Value == domain)
        .Select(kvp => kvp.Key)
        .ToList();
  }
}