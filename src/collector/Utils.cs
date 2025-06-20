
class Utils
{
  public static List<UnitToProcess> GetAllUnitsToProcess()
  {
    var unitsToProcess = new List<UnitToProcess>();

    var orderedRegions = Constants.Regions;
    var queueTypes = Constants.QueueTypes;
    var orderedTiers = Constants.Tiers;

    // Order divisions with lower Roman numerals first
    var orderedDivisions = Constants.Divisions;

    foreach (var region in orderedRegions)
    {
      foreach (var queueType in queueTypes)
      {
        foreach (var tier in orderedTiers)
        {
          if (Constants.HighTiers.Contains(tier))
          {
            unitsToProcess.Add(new UnitToProcess
            {
              Region = region,
              MatchRegion = Constants.RegionToMatchRegion[region],
              QueueType = queueType,
              Tier = tier,
              Division = "I",
            });
          }
          else
          {
            foreach (var division in orderedDivisions)
            {
              unitsToProcess.Add(new UnitToProcess
              {
                Region = region,
                MatchRegion = Constants.RegionToMatchRegion[region],
                QueueType = queueType,
                Tier = tier,
                Division = division,
              });
            }
          }
        }
      }
    }

    return unitsToProcess;
  }

  /// <summary>
  /// Common activity time and rate limit constants
  /// </summary>
  public static class ActivityConstants
  {
    public static readonly TimeSpan MaxActivityTime = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan ShortRateLimitThreshold = TimeSpan.FromSeconds(5);
    public static readonly int MatchBatchSize = 25;
    public static readonly int MaxMatchesToFetch = 120;
    public static readonly double RateLimit2MinThreshold = 0.9;
  }

  /// <summary>
  /// Checks if activity should continue based on time limits
  /// </summary>
  public static bool ShouldContinueActivity(DateTime activityStartTime)
  {
    return DateTime.UtcNow - activityStartTime < ActivityConstants.MaxActivityTime;
  }    /// <summary>
       /// Determines if a rate limit wait time is acceptable for inline waiting
       /// </summary>

  public static bool IsShortRateLimit(TimeSpan waitTime)
  {
    return waitTime <= ActivityConstants.ShortRateLimitThreshold;
  }

  /// <summary>
  /// Determines if rate limit should stop API calls
  /// </summary>
  public static bool ShouldStopForRateLimit(TimeSpan waitTime)
  {
    return waitTime > ActivityConstants.ShortRateLimitThreshold;
  }
}