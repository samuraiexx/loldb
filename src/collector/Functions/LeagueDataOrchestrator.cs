using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using collector.Models;

namespace collector.Functions;

public static class LeagueDataOrchestrator
{
  [Function("LeagueDataOrchestrator")]
  public static async Task<string> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
  {
    var logger = context.CreateReplaySafeLogger("LeagueDataOrchestrator");
    logger.LogInformation("Starting League Data Collection Orchestrator");

    try
    {
      // Initialize processing states for all combinations
      var processingStates = InitializeProcessingStates();
      logger.LogInformation("Initialized {Count} processing states", processingStates.Count);

      var orchestrationStartTime = DateTime.UtcNow;
      var maxRunTime = TimeSpan.FromHours(23); // Safety limit for long-running orchestration

      while (!AllStatesCompleted(processingStates) &&
             DateTime.UtcNow - orchestrationStartTime < maxRunTime)
      {
        logger.LogInformation("Starting new processing cycle");

        // Group states by region for parallel processing
        var statesByRegion = processingStates
            .Where(s => !s.IsCompleted)
            .GroupBy(s => s.Region)
            .ToList();

        logger.LogInformation("Processing {RegionCount} regions in parallel", statesByRegion.Count());

        // Process each region in parallel
        var regionTasks = statesByRegion.Select(regionGroup =>
            context.CallActivityAsync<List<ProcessingState>>(
                "ProcessRegionActivity",
                regionGroup.ToList()
            )
        ).ToArray();

        var regionResults = await Task.WhenAll(regionTasks);

        // Update processing states
        var updatedStates = regionResults.SelectMany(states => states).ToList();
        foreach (var updatedState in updatedStates)
        {
          var existingState = processingStates.FirstOrDefault(s =>
              s.Region == updatedState.Region &&
              s.QueueType == updatedState.QueueType &&
              s.Tier == updatedState.Tier &&
              s.Division == updatedState.Division);

          if (existingState != null)
          {
            existingState.Page = updatedState.Page;
            existingState.IsCompleted = updatedState.IsCompleted;
            existingState.LastProcessed = updatedState.LastProcessed;
            existingState.TotalProcessed = updatedState.TotalProcessed;
          }
        }

        // Log progress
        var completedCount = processingStates.Count(s => s.IsCompleted);
        var totalCount = processingStates.Count;
        var totalProcessedEntries = processingStates.Sum(s => s.TotalProcessed);

        logger.LogInformation("Progress: {Completed}/{Total} states completed, {TotalEntries} total entries processed",
            completedCount, totalCount, totalProcessedEntries);

        // If not all completed, wait before next cycle
        if (!AllStatesCompleted(processingStates))
        {
          logger.LogInformation("Waiting 2 minutes before next cycle due to rate limits");
          await context.CreateTimer(DateTime.UtcNow.AddMinutes(2), CancellationToken.None);
        }
      }
      var finalCompletedCount = processingStates.Count(s => s.IsCompleted);
      var finalTotalEntries = processingStates.Sum(s => s.TotalProcessed);

      logger.LogInformation("Orchestration completed. {Completed}/{Total} states completed, {TotalEntries} total entries processed",
          finalCompletedCount, processingStates.Count, finalTotalEntries);

      return $"Orchestration completed. {finalCompletedCount}/{processingStates.Count} states completed, {finalTotalEntries} total entries processed";
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error in orchestration");
      throw;
    }
  }

  private static List<ProcessingState> InitializeProcessingStates()
  {
    var states = new List<ProcessingState>();

    foreach (var region in Constants.Regions)
    {
      foreach (var queueType in Constants.QueueTypes)
      {
        foreach (var tier in Constants.Tiers)
        {
          if (Constants.HighTiers.Contains(tier))
          {
            // High tiers only have one division
            states.Add(new ProcessingState
            {
              Region = region,
              QueueType = queueType,
              Tier = tier,
              Division = "I",
              Page = 1,
              LastProcessed = DateTime.UtcNow
            });
          }
          else
          {
            // Regular tiers have divisions I-IV
            foreach (var division in Constants.Divisions)
            {
              states.Add(new ProcessingState
              {
                Region = region,
                QueueType = queueType,
                Tier = tier,
                Division = division,
                Page = 1,
                LastProcessed = DateTime.UtcNow
              });
            }
          }
        }
      }
    }

    return states;
  }

  private static bool AllStatesCompleted(List<ProcessingState> states)
  {
    return states.All(s => s.IsCompleted);
  }
}
