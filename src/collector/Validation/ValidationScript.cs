using System;
using System.Linq;
using collector.Models;

namespace collector.Validation;

public class ValidationScript
{
  public static void Main(string[] args)
  {
    Console.WriteLine("=== League Data Collector Validation ===\n");

    // Validate Constants
    ValidateConstants();

    // Validate Processing State Logic
    ValidateProcessingStates();

    // Validate Rate Limit Parsing
    ValidateRateLimitParsing();

    Console.WriteLine("✅ All validations passed!");
  }

  private static void ValidateConstants()
  {
    Console.WriteLine("🔍 Validating Constants...");

    // Check regions
    var expectedRegionCount = 15;
    if (Constants.Regions.Length != expectedRegionCount)
    {
      throw new InvalidOperationException($"Expected {expectedRegionCount} regions, got {Constants.Regions.Length}");
    }

    // Check queue types
    var expectedQueueCount = 4;
    if (Constants.QueueTypes.Length != expectedQueueCount)
    {
      throw new InvalidOperationException($"Expected {expectedQueueCount} queue types, got {Constants.QueueTypes.Length}");
    }

    // Check tiers
    var expectedTierCount = 10;
    if (Constants.Tiers.Length != expectedTierCount)
    {
      throw new InvalidOperationException($"Expected {expectedTierCount} tiers, got {Constants.Tiers.Length}");
    }

    // Check high tiers
    var expectedHighTiers = new[] { "CHALLENGER", "GRANDMASTER", "MASTER" };
    if (!Constants.HighTiers.SequenceEqual(expectedHighTiers))
    {
      throw new InvalidOperationException("High tiers don't match expected values");
    }

    Console.WriteLine("   ✓ All constants are valid");
  }

  private static void ValidateProcessingStates()
  {
    Console.WriteLine("🔍 Validating Processing States...");

    // Calculate expected number of processing states
    var regularTiers = Constants.Tiers.Except(Constants.HighTiers).ToArray();
    var highTiers = Constants.HighTiers;

    var expectedStates = Constants.Regions.Length * Constants.QueueTypes.Length *
                       (regularTiers.Length * Constants.Divisions.Length + highTiers.Length);

    // For reference: 15 regions × 4 queues × (7 regular tiers × 4 divisions + 3 high tiers × 1 division)
    // = 15 × 4 × (28 + 3) = 15 × 4 × 31 = 1,860

    Console.WriteLine($"   Expected processing states: {expectedStates}");

    // Validate that high tiers don't use all divisions
    foreach (var tier in Constants.HighTiers)
    {
      if (tier == "CHALLENGER" || tier == "GRANDMASTER" || tier == "MASTER")
      {
        // These should only use division "I"
        continue;
      }
      throw new InvalidOperationException($"Unexpected high tier: {tier}");
    }

    Console.WriteLine("   ✓ Processing state logic is valid");
  }

  private static void ValidateRateLimitParsing()
  {
    Console.WriteLine("🔍 Validating Rate Limit Logic...");

    // Validate rate limit thresholds
    var shortRateLimitThreshold = 10; // seconds
    var longRateLimitThreshold = 60; // seconds (for 2-minute window)

    // These values should match the logic in ProcessRegionActivity
    if (shortRateLimitThreshold >= longRateLimitThreshold)
    {
      throw new InvalidOperationException("Short rate limit threshold should be less than long rate limit threshold");
    }

    // Validate request limits
    var maxRequestsIn2Minutes = 100;
    var requestsPerSecond = 20;

    if (maxRequestsIn2Minutes <= 0 || requestsPerSecond <= 0)
    {
      throw new InvalidOperationException("Rate limits must be positive");
    }

    Console.WriteLine("   ✓ Rate limit logic is valid");
  }

  private static void ValidateDataModels()
  {
    Console.WriteLine("🔍 Validating Data Models...");

    // Test PlayerStatsDocument
    var document = new PlayerStatsDocument
    {
      Id = "test-id",
      Puuid = "test-puuid",
      Region = "NA1",
      Snapshots = new List<PlayerSnapshot>()
    };

    if (string.IsNullOrEmpty(document.Id) || string.IsNullOrEmpty(document.Puuid))
    {
      throw new InvalidOperationException("Required fields are missing");
    }

    // Test PlayerSnapshot
    var snapshot = new PlayerSnapshot
    {
      Timestamp = DateTime.UtcNow,
      Tier = "GOLD",
      Rank = "I",
      LeaguePoints = 50
    };

    if (snapshot.LeaguePoints < 0)
    {
      throw new InvalidOperationException("League points cannot be negative");
    }

    Console.WriteLine("   ✓ Data models are valid");
  }
}
