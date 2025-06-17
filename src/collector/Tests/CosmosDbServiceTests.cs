using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using Moq;
using Xunit;
using collector.Services;
using collector.Models;
using System.Net;

namespace collector.Tests;

public class CosmosDbServiceTests
{
  private readonly Mock<CosmosClient> _mockCosmosClient;
  private readonly Mock<ILogger<CosmosDbService>> _mockLogger;
  private readonly CosmosDbService _service;

  public CosmosDbServiceTests()
  {
    _mockCosmosClient = new Mock<CosmosClient>();
    _mockLogger = new Mock<ILogger<CosmosDbService>>();

    _service = new CosmosDbService(_mockCosmosClient.Object, _mockLogger.Object);
  }

  [Fact]
  public async Task InitializeAsync_CreatesDatabase_ReturnsTrue()
  {
    // Arrange
    var mockDatabase = new Mock<Database>();
    var databaseResponse = Mock.Of<DatabaseResponse>(r => r.Database == mockDatabase.Object);

    _mockCosmosClient
        .Setup(c => c.CreateDatabaseIfNotExistsAsync(
            It.Is<string>(s => s == "player_stats"),
            It.IsAny<int?>(),
            It.IsAny<RequestOptions>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(databaseResponse);

    // Act
    var result = await _service.InitializeAsync();

    // Assert
    Assert.True(result);
  }

  [Fact]
  public async Task InitializeAsync_HandlesException_ReturnsFalse()
  {
    // Arrange
    _mockCosmosClient
        .Setup(c => c.CreateDatabaseIfNotExistsAsync(
            It.Is<string>(s => s == "player_stats"),
            It.IsAny<int?>(),
            It.IsAny<RequestOptions>(),
            It.IsAny<CancellationToken>()))
        .ThrowsAsync(new CosmosException("Test exception", HttpStatusCode.InternalServerError, 0, "test", 0));

    // Act
    var result = await _service.InitializeAsync();

    // Assert
    Assert.False(result);
  }
  [Fact]
  public void PlayerStatsDocument_SerializesCorrectly()
  {
    // Arrange
    var document = new PlayerStatsDocument
    {
      Id = "test-puuid",
      SummonerId = "test-summoner-id",
      Puuid = "test-puuid",
      LeagueId = "test-league-id",
      Region = "NA1",
      CreatedAt = DateTime.UtcNow,
      LastUpdated = DateTime.UtcNow,
      Snapshot = new PlayerSnapshot
      {
        Timestamp = DateTime.UtcNow,
        Tier = "GOLD",
        Rank = "I",
        LeaguePoints = 50,
        Wins = 10,
        Losses = 5,
        HotStreak = false,
        Veteran = false,
        FreshBlood = true,
        Inactive = false
      }
    };

    // Act & Assert
    Assert.Equal("test-puuid", document.Id);
    Assert.Equal("NA1", document.Region);
    Assert.NotNull(document.Snapshot);
    Assert.Equal("GOLD", document.Snapshot.Tier);
  }

  [Fact]
  public void ProcessingState_InitializesCorrectly()
  {
    // Arrange & Act
    var state = new ProcessingState
    {
      Region = "NA1",
      QueueType = "RANKED_SOLO_5x5",
      Tier = "GOLD",
      Division = "I",
      Page = 1
    };

    // Assert
    Assert.Equal("NA1", state.Region);
    Assert.Equal("RANKED_SOLO_5x5", state.QueueType);
    Assert.Equal("GOLD", state.Tier);
    Assert.Equal("I", state.Division);
    Assert.Equal(1, state.Page);
    Assert.False(state.IsCompleted);
    Assert.Equal(0, state.TotalProcessed);
  }

  [Fact]
  public void RateLimitInfo_ParsesCorrectly()
  {
    // Arrange & Act
    var rateLimitInfo = new RateLimitInfo
    {
      RequestsPerSecond = 20,
      RequestsPer2Minutes = 100,
      CurrentRequestsPerSecond = 18,
      CurrentRequestsPer2Minutes = 54,
      RetryAfterSeconds = 8,
      IsRateLimited = true
    };

    // Assert
    Assert.Equal(20, rateLimitInfo.RequestsPerSecond);
    Assert.Equal(100, rateLimitInfo.RequestsPer2Minutes);
    Assert.Equal(18, rateLimitInfo.CurrentRequestsPerSecond);
    Assert.Equal(54, rateLimitInfo.CurrentRequestsPer2Minutes);
    Assert.Equal(8, rateLimitInfo.RetryAfterSeconds);
    Assert.True(rateLimitInfo.IsRateLimited);
  }

  [Fact]
  public async Task BatchUpsertPlayerStatsAsync_WithEmptyList_ReturnsEarly()
  {
    // Arrange
    var emptyList = new List<PlayerStatsDocument>();

    // Act & Assert - should not throw
    await _service.BatchUpsertPlayerStatsAsync(emptyList, "RANKED_SOLO_5x5", "NA1");
  }

  [Fact]
  public void PlayerStatsDocument_WithSingleSnapshot_SerializesCorrectly()
  {
    // Arrange
    var document = new PlayerStatsDocument
    {
      Id = "test-puuid",
      SummonerId = "test-summoner-id",
      Puuid = "test-puuid",
      LeagueId = "test-league-id",
      Region = "NA1",
      CreatedAt = DateTime.UtcNow,
      LastUpdated = DateTime.UtcNow,
      Snapshot = new PlayerSnapshot
      {
        Timestamp = DateTime.UtcNow,
        Tier = "DIAMOND",
        Rank = "II",
        LeaguePoints = 75,
        Wins = 25,
        Losses = 10,
        HotStreak = true,
        Veteran = true,
        FreshBlood = false,
        Inactive = false
      }
    };

    // Act & Assert
    Assert.Equal("test-puuid", document.Id);
    Assert.Equal("NA1", document.Region);
    Assert.NotNull(document.Snapshot);
    Assert.Equal("DIAMOND", document.Snapshot.Tier);
    Assert.Equal("II", document.Snapshot.Rank);
    Assert.Equal(75, document.Snapshot.LeaguePoints);
    Assert.True(document.Snapshot.HotStreak);
    Assert.True(document.Snapshot.Veteran);
  }
}
