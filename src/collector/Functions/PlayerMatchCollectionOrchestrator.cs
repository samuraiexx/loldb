
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

public class PlayerMatchCollectionOrchestrator
{
  [Function("PlayerMatchCollectionOrchestrator")]
  public async Task RunOrchestratorAsync([OrchestrationTrigger] TaskOrchestrationContext context)
  {
    var logger = context.CreateReplaySafeLogger("PlayerMatchCollectionOrchestrator");
    logger.LogInformation("Starting Match Collection Orchestrator");

    var maxMatchesPerUnit = context.GetInput<int>();
    logger.LogInformation("Configuration: Max matches per unit = {MaxMatches}", maxMatchesPerUnit);

    // Initialize processing states for all combinations
    var unitsToProcess = Utils.GetAllUnitsToProcess();
    logger.LogInformation("Initialized {Count} units to process", unitsToProcess.Count); var PlayerMatchProcessingState = unitsToProcess
      .GroupBy(scope => scope.MatchRegion)
      .Select(scope => new PlayerMatchProcessingState
      {
        ProcessingScope = scope.ToList(),
        MaxMatchesPerUnit = maxMatchesPerUnit
      })
      .ToList();

    while (!PlayerMatchProcessingState.All(state => state.TotalProcessed == state.ProcessingScope.Count()))
    {
      var endOfRateLimit = PlayerMatchProcessingState.Max(scope => scope.EndOfRateLimit);

      // Only wait if we have a valid future rate limit time
      if (endOfRateLimit > context.CurrentUtcDateTime)
      {
        logger.LogInformation($"Waiting {(endOfRateLimit - context.CurrentUtcDateTime).TotalSeconds:F1} seconds before next cycle due to rate limits");
        await context.CreateTimer(endOfRateLimit, CancellationToken.None);
      }

      logger.LogInformation("Starting new processing cycle");
      logger.LogInformation("Processing {RegionCount} regions in parallel", PlayerMatchProcessingState.Count);

      // Process each region in parallel
      var regionTasks = PlayerMatchProcessingState.Select(regionGroup =>
          context.CallActivityAsync<PlayerMatchProcessingState>(
              "CollectMatchesForDomainActivity",
              regionGroup,
              Utils.GetTaskOptions()
          )
      ).ToArray();

      var activityResults = await Task.WhenAll(regionTasks);
      PlayerMatchProcessingState = activityResults.ToList();

      var totalProcessedEntries = PlayerMatchProcessingState.Sum(state => state.TotalProcessed);
      var totalCount = PlayerMatchProcessingState.Sum(state => state.ProcessingScope.Count());

      // Log progress Per Region
      logger.LogInformation("=== Region Progress Details ===");
      foreach (var regionState in PlayerMatchProcessingState)
      {
        var regionName = regionState.ProcessingScope.FirstOrDefault()?.MatchRegion ?? "Unknown";
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