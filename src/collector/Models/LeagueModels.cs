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

  [JsonProperty("snapshot")]
  public PlayerSnapshot? Snapshot { get; set; }

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

  // Match v5 regional domains
  public static readonly Dictionary<string, string> RegionToDomain = new()
  {
    { "NA1", "americas" }, { "BR1", "americas" }, { "LA1", "americas" }, { "LA2", "americas" },
    { "KR", "asia" }, { "JP1", "asia" },
    { "EUW1", "europe" }, { "EUN1", "europe" }, { "ME1", "europe" }, { "TR1", "europe" }, { "RU", "europe" },
    { "OC1", "sea" }, { "SG2", "sea" }, { "TW2", "sea" }, { "VN2", "sea" }
  };

  public static readonly string[] Domains = { "americas", "asia", "europe", "sea" };
}

// Match collection models
public class MatchCollectionState
{
  public string Domain { get; set; } = string.Empty;
  public DateTime StartTime { get; set; }
  public DateTime EndTime { get; set; }
  public List<string> Puuids { get; set; } = new();
  public bool IsCompleted { get; set; } = false;
  public DateTime LastProcessed { get; set; }
  public int TotalMatchesCollected { get; set; } = 0;
}

public class MatchDocument
{
  [JsonProperty("id")]
  public string Id { get; set; } = string.Empty; // matchId

  [JsonProperty("matchId")]
  public string MatchId { get; set; } = string.Empty;

  [JsonProperty("region")]
  public string Region { get; set; } = string.Empty; // partition key

  [JsonProperty("processed")]
  public bool Processed { get; set; } = false;

  [JsonProperty("created_at")]
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  [JsonProperty("match_data")]
  public MatchDto? MatchData { get; set; }
}

public class MatchCollectionConfig
{
  [JsonProperty("start_time")]
  public DateTime StartTime { get; set; } = DateTime.UtcNow.AddDays(-1);
}

// Match v5 API models
public class MatchDto
{
  [JsonProperty("metadata")]
  public MetadataDto Metadata { get; set; } = new();

  [JsonProperty("info")]
  public InfoDto Info { get; set; } = new();
}

public class MetadataDto
{
  [JsonProperty("dataVersion")]
  public string DataVersion { get; set; } = string.Empty;

  [JsonProperty("matchId")]
  public string MatchId { get; set; } = string.Empty;

  [JsonProperty("participants")]
  public List<string> Participants { get; set; } = new();
}

public class InfoDto
{
  [JsonProperty("gameCreation")]
  public long GameCreation { get; set; }

  [JsonProperty("gameDuration")]
  public long GameDuration { get; set; }

  [JsonProperty("gameEndTimestamp")]
  public long GameEndTimestamp { get; set; }

  [JsonProperty("gameId")]
  public long GameId { get; set; }

  [JsonProperty("gameMode")]
  public string GameMode { get; set; } = string.Empty;

  [JsonProperty("gameName")]
  public string GameName { get; set; } = string.Empty;

  [JsonProperty("gameStartTimestamp")]
  public long GameStartTimestamp { get; set; }

  [JsonProperty("gameType")]
  public string GameType { get; set; } = string.Empty;

  [JsonProperty("gameVersion")]
  public string GameVersion { get; set; } = string.Empty;

  [JsonProperty("mapId")]
  public int MapId { get; set; }

  [JsonProperty("participants")]
  public List<ParticipantDto> Participants { get; set; } = new();

  [JsonProperty("platformId")]
  public string PlatformId { get; set; } = string.Empty;

  [JsonProperty("queueId")]
  public int QueueId { get; set; }

  [JsonProperty("teams")]
  public List<TeamDto> Teams { get; set; } = new();

  [JsonProperty("tournamentCode")]
  public string TournamentCode { get; set; } = string.Empty;
}

public class ParticipantDto
{
  [JsonProperty("puuid")]
  public string Puuid { get; set; } = string.Empty;

  [JsonProperty("participantId")]
  public int ParticipantId { get; set; }

  [JsonProperty("teamId")]
  public int TeamId { get; set; }

  [JsonProperty("championId")]
  public int ChampionId { get; set; }

  [JsonProperty("championName")]
  public string ChampionName { get; set; } = string.Empty;

  [JsonProperty("kills")]
  public int Kills { get; set; }

  [JsonProperty("deaths")]
  public int Deaths { get; set; }

  [JsonProperty("assists")]
  public int Assists { get; set; }

  [JsonProperty("win")]
  public bool Win { get; set; }

  // Add other participant fields as needed
}

public class TeamDto
{
  [JsonProperty("teamId")]
  public int TeamId { get; set; }

  [JsonProperty("win")]
  public bool Win { get; set; }

  [JsonProperty("bans")]
  public List<BanDto> Bans { get; set; } = new();
}

public class BanDto
{
  [JsonProperty("championId")]
  public int ChampionId { get; set; }

  [JsonProperty("pickTurn")]
  public int PickTurn { get; set; }
}
