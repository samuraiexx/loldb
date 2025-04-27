using Newtonsoft.Json;

namespace collector.Models;

public class LeagueEntryDTO
{
  [JsonProperty("leagueId")]
  public string LeagueId { get; set; } = string.Empty;

  [JsonProperty("summonerId")]
  public string SummonerId { get; set; } = string.Empty;

  [JsonProperty("puuid")]
  public string Puuid { get; set; } = string.Empty;

  [JsonProperty("queueType")]
  public string QueueType { get; set; } = string.Empty;

  [JsonProperty("tier")]
  public string Tier { get; set; } = string.Empty;

  [JsonProperty("rank")]
  public string Rank { get; set; } = string.Empty;

  [JsonProperty("leaguePoints")]
  public int LeaguePoints { get; set; }

  [JsonProperty("wins")]
  public int Wins { get; set; }

  [JsonProperty("losses")]
  public int Losses { get; set; }

  [JsonProperty("hotStreak")]
  public bool HotStreak { get; set; }

  [JsonProperty("veteran")]
  public bool Veteran { get; set; }

  [JsonProperty("freshBlood")]
  public bool FreshBlood { get; set; }

  [JsonProperty("inactive")]
  public bool Inactive { get; set; }

  [JsonProperty("miniSeries")]
  public MiniSeriesDTO? MiniSeries { get; set; }
}

public class MiniSeriesDTO
{
  [JsonProperty("losses")]
  public int Losses { get; set; }

  [JsonProperty("progress")]
  public string Progress { get; set; } = string.Empty;

  [JsonProperty("target")]
  public int Target { get; set; }

  [JsonProperty("wins")]
  public int Wins { get; set; }
}

public class PlayerSnapshot
{
  [JsonProperty("timestamp")]
  public DateTime Timestamp { get; set; }

  [JsonProperty("tier")]
  public string Tier { get; set; } = string.Empty;

  [JsonProperty("rank")]
  public string Rank { get; set; } = string.Empty;

  [JsonProperty("league_points")]
  public int LeaguePoints { get; set; }

  [JsonProperty("wins")]
  public int Wins { get; set; }

  [JsonProperty("losses")]
  public int Losses { get; set; }

  [JsonProperty("hot_streak")]
  public bool HotStreak { get; set; }

  [JsonProperty("veteran")]
  public bool Veteran { get; set; }

  [JsonProperty("fresh_blood")]
  public bool FreshBlood { get; set; }

  [JsonProperty("inactive")]
  public bool Inactive { get; set; }

  [JsonProperty("mini_series")]
  public MiniSeriesDTO? MiniSeries { get; set; }
}

public class PlayerStatsDocument
{
  [JsonProperty("id")]
  public string Id { get; set; } = string.Empty;

  [JsonProperty("summoner_id")]
  public string SummonerId { get; set; } = string.Empty;

  [JsonProperty("puuid")]
  public string Puuid { get; set; } = string.Empty;

  [JsonProperty("league_id")]
  public string LeagueId { get; set; } = string.Empty;

  [JsonProperty("snapshots")]
  public List<PlayerSnapshot> Snapshots { get; set; } = new();

  [JsonProperty("created_at")]
  public DateTime CreatedAt { get; set; }

  [JsonProperty("last_updated")]
  public DateTime LastUpdated { get; set; }

  [JsonProperty("region")]
  public string Region { get; set; } = string.Empty;
}

public class ProcessingState
{
  public string Region { get; set; } = string.Empty;
  public string QueueType { get; set; } = string.Empty;
  public string Tier { get; set; } = string.Empty;
  public string Division { get; set; } = string.Empty;
  public int Page { get; set; } = 1;
  public bool IsCompleted { get; set; } = false;
  public DateTime LastProcessed { get; set; }
  public int TotalProcessed { get; set; } = 0;
}

public class RateLimitInfo
{
  public int RequestsPerSecond { get; set; }
  public int RequestsPer2Minutes { get; set; }
  public int CurrentRequestsPerSecond { get; set; }
  public int CurrentRequestsPer2Minutes { get; set; }
  public int RetryAfterSeconds { get; set; }
  public bool IsRateLimited { get; set; }
}

public static class Constants
{
  public static readonly string[] Regions = { "BR1", "EUN1", "EUW1", "JP1", "KR", "LA1", "LA2", "ME1", "NA1", "OC1", "RU", "SG2", "TR1", "TW2", "VN2" };

  public static readonly string[] QueueTypes = { "RANKED_SOLO_5x5", "RANKED_FLEX_SR" };

  public static readonly string[] Tiers = { "CHALLENGER", "GRANDMASTER", "MASTER", "DIAMOND", "EMERALD", "PLATINUM", "GOLD", "SILVER", "BRONZE", "IRON" };

  public static readonly string[] Divisions = { "I", "II", "III", "IV" };

  public static readonly string[] HighTiers = { "CHALLENGER", "GRANDMASTER", "MASTER" };
}
