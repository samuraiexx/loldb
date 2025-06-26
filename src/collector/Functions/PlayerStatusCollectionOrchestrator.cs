using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

public static class PlayerStatusCollectionOrchestrator
{
  [Function("PlayerStatusCollectionOrchestrator")]
  public static async Task RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
  {
    var logger = context.CreateReplaySafeLogger("PlayerStatusCollectionOrchestrator");
    logger.LogInformation("Starting League Data Collection Orchestrator");

    // Initialize processing states for all combinations
    var unitsToProcess = Utils.GetAllUnitsToProcess();
    logger.LogInformation("Initialized {Count} units to process", unitsToProcess.Count());

    var playerStatusProcessingState = unitsToProcess
      .GroupBy(scope => scope.Region)
      .Select(scope => new PlayerStatusProcessingState { ProcessingScope = scope.Select(unit => unit).ToList() })
      .ToList();

    while (!playerStatusProcessingState.All(state => state.TotalProcessed == state.ProcessingScope.Count()))
    {
      var endOfRateLimit = playerStatusProcessingState.Max(scope => scope.EndOfRateLimit);

      // Only wait if we have a valid future rate limit time
      if (endOfRateLimit > context.CurrentUtcDateTime)
      {
        logger.LogInformation($"Waiting {(endOfRateLimit - context.CurrentUtcDateTime).TotalSeconds:F1} seconds before next cycle due to rate limits");
        await context.CreateTimer(endOfRateLimit, CancellationToken.None);
      }

      logger.LogInformation("Starting new processing cycle");
      logger.LogInformation("Processing {RegionCount} regions in parallel", playerStatusProcessingState.Count());

      // Process each region in parallel
      var regionTasks = playerStatusProcessingState.Select(regionGroup =>
          context.CallActivityAsync<PlayerStatusProcessingState>(
              "PlayerStatusCollectionActivity",
              regionGroup,
              Utils.GetTaskOptions()
          )
      ).ToArray();

      var activityResults = await Task.WhenAll(regionTasks);
      playerStatusProcessingState = activityResults.ToList();

      var totalProcessedEntries = playerStatusProcessingState.Sum(state => state.TotalProcessed);
      var totalCount = playerStatusProcessingState.Sum(state => state.ProcessingScope.Count());

      // Log progress Per Region
      logger.LogInformation("=== Region Progress Details ===");
      foreach (var regionState in playerStatusProcessingState)
      {
        var regionName = regionState.ProcessingScope.FirstOrDefault()?.Region ?? "Unknown";
        var regionCompleted = regionState.TotalProcessed;
        var regionTotal = regionState.ProcessingScope.Count();
        var regionProgress = regionTotal > 0 ? (double)regionCompleted / regionTotal * 100 : 0;

        logger.LogInformation("Region {Region}: {Completed}/{Total} processed ({Progress:F1}%)",
            regionName, regionCompleted, regionTotal, regionProgress);
      }
      logger.LogInformation("=== End Region Progress ===");
    }

    logger.LogInformation("Orchestration completed.");
  }

}
