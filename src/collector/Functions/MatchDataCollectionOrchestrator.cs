
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

public class MatchDataCollectionOrchestrator
{
  [Function("MatchDataCollectionOrchestrator")]
  public async Task RunOrchestratorAsync([OrchestrationTrigger] TaskOrchestrationContext context)
  {
    var logger = context.CreateReplaySafeLogger("MatchDataCollectionOrchestrator");
    logger.LogInformation("Starting Match Data Collection Orchestrator");

    var maxCreatedOn = context.CurrentUtcDateTime;
    var totalCountPerMatchRegion = await context.CallActivityAsync<Dictionary<string, int>>("GetMatchCollectionTotalCountActivity", string.Empty);

    var matchDataProcessingState = Constants.MatchRegions
      .Select(matchRegion => new MatchDataProcessingState { MatchRegion = matchRegion, MaxCreatedOn = maxCreatedOn, TotalToProcess = totalCountPerMatchRegion[matchRegion] })
      .ToList();

    while (!matchDataProcessingState.All(state => state.TotalProcessed == state.TotalToProcess))
    {
      var endOfRateLimit = matchDataProcessingState.Max(scope => scope.EndOfRateLimit);

      // Only wait if we have a valid future rate limit time
      if (endOfRateLimit > context.CurrentUtcDateTime)
      {
        logger.LogInformation($"Waiting {(endOfRateLimit - context.CurrentUtcDateTime).TotalSeconds:F1} seconds before next cycle due to rate limits");
        await context.CreateTimer(endOfRateLimit, CancellationToken.None);
      }

      logger.LogInformation("Starting new processing cycle");
      logger.LogInformation("Processing {RegionCount} regions in parallel", matchDataProcessingState.Count);

      // Process each region in parallel
      var regionTasks = matchDataProcessingState.Select(processingState => context.CallActivityAsync<MatchDataProcessingState>(
              "CollectMatchDataActivity",
              processingState
          )
      ).ToArray();

      var activityResults = await Task.WhenAll(regionTasks);
      matchDataProcessingState = activityResults.ToList();

      // Log progress Per Region
      logger.LogInformation("=== Region Progress Details ===");
      foreach (var regionState in matchDataProcessingState)
      {
        var regionName = regionState.MatchRegion ?? "Unknown";
        var regionCompleted = regionState.TotalProcessed;
        var regionTotal = regionState.TotalToProcess;
        var regionProgress = regionTotal > 0 ? (double)regionCompleted / regionTotal * 100 : 0;

        logger.LogInformation("Region {Region}: {Completed}/{Total} processed ({Progress:F1}%)",
            regionName, regionCompleted, regionTotal, regionProgress);
      }
      logger.LogInformation("=== End Region Progress ===");
    }
    logger.LogInformation("Orchestration completed.");
  }
}