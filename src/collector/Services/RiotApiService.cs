using Microsoft.Extensions.Logging;
using collector.Models;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace collector.Services;

public interface IRiotApiService
{
  Task<(List<LeagueEntryDTO> entries, RateLimitInfo rateLimitInfo)> GetLeagueEntriesAsync(
      string region, string queueType, string tier, string division, int page = 1);
  Task<(List<string> matchIds, RateLimitInfo rateLimitInfo)> GetMatchIdsByPuuidAsync(
      string domain, string puuid, long? startTime = null, long? endTime = null,
      string matchType = "ranked", int start = 0, int count = 20);
  Task<(MatchDto? match, RateLimitInfo rateLimitInfo)> GetMatchAsync(string domain, string matchId);
}

public class RiotApiService : IRiotApiService
{
  private readonly HttpClient _httpClient;
  private readonly ILogger<RiotApiService> _logger;
  private readonly string _apiKey;

  public RiotApiService(HttpClient httpClient, ILogger<RiotApiService> logger)
  {
    _httpClient = httpClient;
    _logger = logger;
    _apiKey = Environment.GetEnvironmentVariable("RIOT_API_KEY") ??
              throw new InvalidOperationException("RIOT_API_KEY environment variable is required");
  }

  public async Task<(List<LeagueEntryDTO> entries, RateLimitInfo rateLimitInfo)> GetLeagueEntriesAsync(
      string region, string queueType, string tier, string division, int page = 1)
  {
    var url = $"https://{region.ToLower()}.api.riotgames.com/lol/league-exp/v4/entries/{queueType}/{tier}/{division}";

    var requestUri = $"{url}?page={page}&api_key={_apiKey}";

    _logger.LogDebug("Making request to: {Region} - {QueueType} {Tier} {Division} Page {Page}",
        region, queueType, tier, division, page);

    try
    {
      var response = await _httpClient.GetAsync(requestUri);
      var rateLimitInfo = ParseRateLimitHeaders(response.Headers);

      if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
      {
        _logger.LogWarning("Rate limit exceeded for {Region}. Retry after: {RetryAfter}s",
            region, rateLimitInfo.RetryAfterSeconds);
        rateLimitInfo.IsRateLimited = true;
        return (new List<LeagueEntryDTO>(), rateLimitInfo);
      }

      response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadAsStringAsync();
      var entries = JsonConvert.DeserializeObject<List<LeagueEntryDTO>>(content) ?? new List<LeagueEntryDTO>();

      _logger.LogDebug("Retrieved {Count} entries for {Region} - {QueueType} {Tier} {Division} Page {Page}",
          entries.Count, region, queueType, tier, division, page);

      return (entries, rateLimitInfo);
    }
    catch (HttpRequestException ex)
    {
      _logger.LogError(ex, "HTTP error getting league entries for {Region} - {QueueType} {Tier} {Division} Page {Page}",
          region, queueType, tier, division, page);
      throw;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting league entries for {Region} - {QueueType} {Tier} {Division} Page {Page}",
          region, queueType, tier, division, page);
      throw;
    }
  }

  private RateLimitInfo ParseRateLimitHeaders(System.Net.Http.Headers.HttpResponseHeaders headers)
  {
    var rateLimitInfo = new RateLimitInfo();

    // Parse X-App-Rate-Limit header (e.g., "100:120,20:1")
    if (headers.TryGetValues("X-App-Rate-Limit", out var appRateLimitValues))
    {
      var appRateLimit = appRateLimitValues.FirstOrDefault();
      if (!string.IsNullOrEmpty(appRateLimit))
      {
        var matches = Regex.Matches(appRateLimit, @"(\d+):(\d+)");
        foreach (Match match in matches)
        {
          var requests = int.Parse(match.Groups[1].Value);
          var seconds = int.Parse(match.Groups[2].Value);

          if (seconds == 1)
          {
            rateLimitInfo.RequestsPerSecond = requests;
          }
          else if (seconds == 120)
          {
            rateLimitInfo.RequestsPer2Minutes = requests;
          }
        }
      }
    }

    // Parse X-App-Rate-Limit-Count header (e.g., "54:120,18:1")
    if (headers.TryGetValues("X-App-Rate-Limit-Count", out var appRateLimitCountValues))
    {
      var appRateLimitCount = appRateLimitCountValues.FirstOrDefault();
      if (!string.IsNullOrEmpty(appRateLimitCount))
      {
        var matches = Regex.Matches(appRateLimitCount, @"(\d+):(\d+)");
        foreach (Match match in matches)
        {
          var requests = int.Parse(match.Groups[1].Value);
          var seconds = int.Parse(match.Groups[2].Value);

          if (seconds == 1)
          {
            rateLimitInfo.CurrentRequestsPerSecond = requests;
          }
          else if (seconds == 120)
          {
            rateLimitInfo.CurrentRequestsPer2Minutes = requests;
          }
        }
      }
    }

    // Parse Retry-After header
    if (headers.TryGetValues("Retry-After", out var retryAfterValues))
    {
      var retryAfter = retryAfterValues.FirstOrDefault();
      if (int.TryParse(retryAfter, out var retrySeconds))
      {
        rateLimitInfo.RetryAfterSeconds = retrySeconds;
      }
    }

    return rateLimitInfo;
  }

  public async Task<(List<string> matchIds, RateLimitInfo rateLimitInfo)> GetMatchIdsByPuuidAsync(
      string domain, string puuid, long? startTime = null, long? endTime = null,
      string matchType = "ranked", int start = 0, int count = 20)
  {
    var url = $"https://{domain.ToLower()}.api.riotgames.com/lol/match/v5/matches/by-puuid/{puuid}/ids";

    var queryParams = new List<string> { $"api_key={_apiKey}" };

    if (startTime.HasValue)
      queryParams.Add($"startTime={startTime.Value}");
    if (endTime.HasValue)
      queryParams.Add($"endTime={endTime.Value}");
    if (!string.IsNullOrEmpty(matchType))
      queryParams.Add($"type={matchType}");
    if (start > 0)
      queryParams.Add($"start={start}");
    if (count != 20)
      queryParams.Add($"count={count}");

    var requestUri = $"{url}?{string.Join("&", queryParams)}";

    _logger.LogDebug("Getting match IDs for PUUID {Puuid} from {Domain}, start={Start}, count={Count}",
        puuid, domain, start, count);

    try
    {
      var response = await _httpClient.GetAsync(requestUri);
      var rateLimitInfo = ParseRateLimitHeaders(response.Headers);

      if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
      {
        _logger.LogWarning("Rate limit exceeded for {Domain}. Retry after: {RetryAfter}s",
            domain, rateLimitInfo.RetryAfterSeconds);
        rateLimitInfo.IsRateLimited = true;
        return (new List<string>(), rateLimitInfo);
      }

      response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadAsStringAsync();
      var matchIds = JsonConvert.DeserializeObject<List<string>>(content) ?? new List<string>();

      _logger.LogDebug("Retrieved {Count} match IDs for PUUID {Puuid} from {Domain}",
          matchIds.Count, puuid, domain);

      return (matchIds, rateLimitInfo);
    }
    catch (HttpRequestException ex)
    {
      _logger.LogError(ex, "HTTP error getting match IDs for PUUID {Puuid} from {Domain}", puuid, domain);
      throw;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting match IDs for PUUID {Puuid} from {Domain}", puuid, domain);
      throw;
    }
  }

  public async Task<(MatchDto? match, RateLimitInfo rateLimitInfo)> GetMatchAsync(string domain, string matchId)
  {
    var url = $"https://{domain.ToLower()}.api.riotgames.com/lol/match/v5/matches/{matchId}";
    var requestUri = $"{url}?api_key={_apiKey}";

    _logger.LogDebug("Getting match details for {MatchId} from {Domain}", matchId, domain);

    try
    {
      var response = await _httpClient.GetAsync(requestUri);
      var rateLimitInfo = ParseRateLimitHeaders(response.Headers);

      if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
      {
        _logger.LogWarning("Rate limit exceeded for {Domain}. Retry after: {RetryAfter}s",
            domain, rateLimitInfo.RetryAfterSeconds);
        rateLimitInfo.IsRateLimited = true;
        return (null, rateLimitInfo);
      }

      response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadAsStringAsync();
      var match = JsonConvert.DeserializeObject<MatchDto>(content);

      _logger.LogDebug("Retrieved match details for {MatchId} from {Domain}", matchId, domain);

      return (match, rateLimitInfo);
    }
    catch (HttpRequestException ex)
    {
      _logger.LogError(ex, "HTTP error getting match details for {MatchId} from {Domain}", matchId, domain);
      throw;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting match details for {MatchId} from {Domain}", matchId, domain);
      throw;
    }
  }
}
