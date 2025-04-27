using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using collector.Services;
using collector.Models;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace collector.Tests;

public class RiotApiServiceTests
{
  private readonly Mock<ILogger<RiotApiService>> _mockLogger;
  private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
  private readonly HttpClient _httpClient;
  private readonly RiotApiService _service;

  public RiotApiServiceTests()
  {
    _mockLogger = new Mock<ILogger<RiotApiService>>();
    _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
    _httpClient = new HttpClient(_mockHttpMessageHandler.Object);

    Environment.SetEnvironmentVariable("RIOT_API_KEY", "test-api-key");
    _service = new RiotApiService(_httpClient, _mockLogger.Object);
  }

  [Fact]
  public async Task GetLeagueEntriesAsync_ReturnsEntriesSuccessfully()
  {
    // Arrange
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

    var jsonResponse = JsonConvert.SerializeObject(testEntries);
    var response = new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
    };

    response.Headers.Add("X-App-Rate-Limit", "100:120,20:1");
    response.Headers.Add("X-App-Rate-Limit-Count", "54:120,18:1");

    _mockHttpMessageHandler
        .Protected()
        .Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
        .ReturnsAsync(response);

    // Act
    var result = await _service.GetLeagueEntriesAsync("NA1", "RANKED_SOLO_5x5", "GOLD", "I", 1);

    // Assert
    Assert.Single(result.entries);
    Assert.Equal("test-puuid", result.entries[0].Puuid);
    Assert.Equal(100, result.rateLimitInfo.RequestsPer2Minutes);
    Assert.Equal(20, result.rateLimitInfo.RequestsPerSecond);
    Assert.Equal(54, result.rateLimitInfo.CurrentRequestsPer2Minutes);
    Assert.Equal(18, result.rateLimitInfo.CurrentRequestsPerSecond);
  }

  [Fact]
  public async Task GetLeagueEntriesAsync_HandlesRateLimitCorrectly()
  {
    // Arrange
    var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
    response.Headers.Add("Retry-After", "8");
    response.Headers.Add("X-App-Rate-Limit", "100:120,20:1");
    response.Headers.Add("X-App-Rate-Limit-Count", "100:120,20:1");

    _mockHttpMessageHandler
        .Protected()
        .Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
        .ReturnsAsync(response);

    // Act
    var result = await _service.GetLeagueEntriesAsync("NA1", "RANKED_SOLO_5x5", "GOLD", "I", 1);

    // Assert
    Assert.Empty(result.entries);
    Assert.True(result.rateLimitInfo.IsRateLimited);
    Assert.Equal(8, result.rateLimitInfo.RetryAfterSeconds);
  }

  [Fact]
  public void Constants_ContainExpectedValues()
  {
    // Assert
    Assert.Contains("NA1", Constants.Regions);
    Assert.Contains("BR1", Constants.Regions);
    Assert.Contains("RANKED_SOLO_5x5", Constants.QueueTypes);
    Assert.Contains("GOLD", Constants.Tiers);
    Assert.Contains("I", Constants.Divisions);
    Assert.Contains("CHALLENGER", Constants.HighTiers);
    Assert.Contains("MASTER", Constants.HighTiers);
    Assert.Contains("GRANDMASTER", Constants.HighTiers);
  }
}
