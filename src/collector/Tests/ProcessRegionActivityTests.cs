using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using collector.Functions;
using collector.Models;
using collector.Services;

namespace collector.Tests;

public class ProcessRegionActivityTests
{
  private readonly Mock<IRiotApiService> _mockRiotApiService;
  private readonly Mock<ICosmosDbService> _mockCosmosDbService;
  private readonly Mock<ILogger<ProcessRegionActivity>> _mockLogger;
  private readonly ProcessRegionActivity _activity;

  public ProcessRegionActivityTests()
  {
    _mockRiotApiService = new Mock<IRiotApiService>();
    _mockCosmosDbService = new Mock<ICosmosDbService>();
    _mockLogger = new Mock<ILogger<ProcessRegionActivity>>();

    _activity = new ProcessRegionActivity(
        _mockRiotApiService.Object,
        _mockCosmosDbService.Object,
        _mockLogger.Object);
  }

  [Fact]
  public async Task RunAsync_ProcessesStatesSuccessfully()
  {
    // Arrange
    var regionStates = new List<ProcessingState>
        {
            new ProcessingState
            {
                Region = "NA1",
                QueueType = "RANKED_SOLO_5x5",
                Tier = "GOLD",
                Division = "I",
                Page = 1,
                IsCompleted = false
            }
        };

    var testEntries = new List<LeagueEntryDTO>
        {
            new LeagueEntryDTO
            {
                LeagueId = "test-league-id",
                SummonerId = "test-summoner-id",
                Puuid = "test-puuid",
                QueueType = "RANKED_SOLO_5x5",
                Tier = "GOLD",
                Rank = "I",
                LeaguePoints = 50,
                Wins = 10,
                Losses = 5
            }
        };

    var rateLimitInfo = new RateLimitInfo
    {
      RequestsPerSecond = 20,
      RequestsPer2Minutes = 100,
      CurrentRequestsPerSecond = 1,
      CurrentRequestsPer2Minutes = 1,
      IsRateLimited = false
    };

    _mockCosmosDbService
        .Setup(s => s.InitializeAsync())
        .ReturnsAsync(true);

    _mockRiotApiService
        .Setup(s => s.GetLeagueEntriesAsync("NA1", "RANKED_SOLO_5x5", "GOLD", "I", 1))
        .ReturnsAsync((testEntries, rateLimitInfo));

    // Return empty entries for page 2 to simulate completion
    _mockRiotApiService
        .Setup(s => s.GetLeagueEntriesAsync("NA1", "RANKED_SOLO_5x5", "GOLD", "I", 2))
        .ReturnsAsync((new List<LeagueEntryDTO>(), rateLimitInfo));

    _mockCosmosDbService
        .Setup(s => s.GetPlayerStatsAsync("test-puuid", "RANKED_SOLO_5x5"))
        .ReturnsAsync((PlayerStatsDocument?)null);

    _mockCosmosDbService
        .Setup(s => s.UpsertPlayerStatsAsync(It.IsAny<PlayerStatsDocument>(), "RANKED_SOLO_5x5"))
        .Returns(Task.CompletedTask);

    // Act
    var result = await _activity.RunAsync(regionStates);

    // Assert
    Assert.Single(result);
    Assert.True(result[0].IsCompleted);
    Assert.Equal(1, result[0].TotalProcessed);
    Assert.Equal(2, result[0].Page);

    _mockRiotApiService.Verify(s => s.GetLeagueEntriesAsync("NA1", "RANKED_SOLO_5x5", "GOLD", "I", 1), Times.Once);
    _mockRiotApiService.Verify(s => s.GetLeagueEntriesAsync("NA1", "RANKED_SOLO_5x5", "GOLD", "I", 2), Times.Once);
    _mockCosmosDbService.Verify(s => s.UpsertPlayerStatsAsync(It.IsAny<PlayerStatsDocument>(), "RANKED_SOLO_5x5"), Times.Once);
  }

  [Fact]
  public async Task RunAsync_HandlesRateLimitCorrectly()
  {
    // Arrange
    var regionStates = new List<ProcessingState>
        {
            new ProcessingState
            {
                Region = "NA1",
                QueueType = "RANKED_SOLO_5x5",
                Tier = "GOLD",
                Division = "I",
                Page = 1,
                IsCompleted = false
            }
        };

    var rateLimitInfo = new RateLimitInfo
    {
      IsRateLimited = true,
      RetryAfterSeconds = 60 // Long rate limit
    };

    _mockCosmosDbService
        .Setup(s => s.InitializeAsync())
        .ReturnsAsync(true);

    _mockRiotApiService
        .Setup(s => s.GetLeagueEntriesAsync("NA1", "RANKED_SOLO_5x5", "GOLD", "I", 1))
        .ReturnsAsync((new List<LeagueEntryDTO>(), rateLimitInfo));

    // Act
    var result = await _activity.RunAsync(regionStates);

    // Assert
    Assert.Single(result);
    Assert.False(result[0].IsCompleted);
    Assert.Equal(1, result[0].Page); // Page should not increment due to rate limit
    Assert.Equal(0, result[0].TotalProcessed);

    _mockRiotApiService.Verify(s => s.GetLeagueEntriesAsync("NA1", "RANKED_SOLO_5x5", "GOLD", "I", 1), Times.Once);
  }

  [Fact]
  public async Task RunAsync_HandlesCompletedStates()
  {
    // Arrange
    var regionStates = new List<ProcessingState>
        {
            new ProcessingState
            {
                Region = "NA1",
                QueueType = "RANKED_SOLO_5x5",
                Tier = "GOLD",
                Division = "I",
                Page = 1,
                IsCompleted = true,
                TotalProcessed = 100
            }
        };

    _mockCosmosDbService
        .Setup(s => s.InitializeAsync())
        .ReturnsAsync(true);

    // Act
    var result = await _activity.RunAsync(regionStates);

    // Assert
    Assert.Single(result);
    Assert.True(result[0].IsCompleted);
    Assert.Equal(100, result[0].TotalProcessed);

    // Should not make any API calls for completed states
    _mockRiotApiService.Verify(s => s.GetLeagueEntriesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
  }
}
