using System.Security;
using Newtonsoft.Json;

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

public class UnitToProcess
{
  public required string Region { get; set; }
  public required string MatchRegion { get; set; } // Match API regional domain
  public required string QueueType { get; set; }
  public required string Tier { get; set; }
  public required string Division { get; set; }
}

public class MatchDataProcessingState
{
  public required string MatchRegion { get; set; }
  public required DateTime MaxCreatedOn { get; set; }
  public required int TotalToProcess { get; set; }
  public int TotalProcessed { get; set; } = 0;
  public DateTime EndOfRateLimit { get; set; } = DateTime.MinValue;
}

public class PlayerMatchProcessingState
{
  public required List<UnitToProcess> ProcessingScope { get; set; }
  public int MaxMatchesPerUnit { get; set; }
  public int TotalProcessed { get; set; } = 0;
  public DateTime EndOfRateLimit { get; set; } = DateTime.MinValue;
}

public class PlayerStatusProcessingState
{
  public required List<UnitToProcess> ProcessingScope { get; set; }
  public int TotalProcessed { get; set; } = 0;
  public int LastProcessedPage { get; set; } = 0;
  public DateTime EndOfRateLimit { get; set; } = DateTime.MinValue;
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
  public static readonly string[] QueueTypes = { "RANKED_SOLO_5x5" };
  public static readonly string[] Tiers = { "CHALLENGER", "GRANDMASTER", "MASTER", "DIAMOND", "EMERALD", "PLATINUM", "GOLD", "SILVER", "BRONZE", "IRON" };
  public static readonly string[] Divisions = { "I", "II", "III", "IV" };
  public static readonly string[] HighTiers = { "CHALLENGER", "GRANDMASTER", "MASTER" };
  public static readonly string[] MatchRegions = { "americas", "asia", "europe", "sea" };

  // Match v5 regional domains
  public static readonly Dictionary<string, string> RegionToMatchRegion = new()
  {
    { "NA1", "americas" }, { "BR1", "americas" }, { "LA1", "americas" }, { "LA2", "americas" },
    { "KR", "asia" }, { "JP1", "asia" },
    { "EUW1", "europe" }, { "EUN1", "europe" }, { "ME1", "europe" }, { "TR1", "europe" }, { "RU", "europe" },
    { "OC1", "sea" }, { "SG2", "sea" }, { "TW2", "sea" }, { "VN2", "sea" }
  };
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
  [JsonProperty("endOfGameResult")]
  public string EndOfGameResult { get; set; } = string.Empty;

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

  [JsonProperty("championSkinId")]
  public int ChampionSkinId { get; set; }

  [JsonProperty("kills")]
  public int Kills { get; set; }

  [JsonProperty("deaths")]
  public int Deaths { get; set; }

  [JsonProperty("assists")]
  public int Assists { get; set; }

  [JsonProperty("win")]
  public bool Win { get; set; }

  // Ping-related fields
  [JsonProperty("allInPings")]
  public int AllInPings { get; set; }
  [JsonProperty("assistMePings")]
  public int AssistMePings { get; set; }

  [JsonProperty("basicPings")]
  public int BasicPings { get; set; }
  [JsonProperty("commandPings")]
  public int CommandPings { get; set; }

  [JsonProperty("dangerPings")]
  public int DangerPings { get; set; }

  [JsonProperty("enemyMissingPings")]
  public int EnemyMissingPings { get; set; }

  [JsonProperty("enemyVisionPings")]
  public int EnemyVisionPings { get; set; }

  [JsonProperty("getBackPings")]
  public int GetBackPings { get; set; }

  [JsonProperty("holdPings")]
  public int HoldPings { get; set; }

  [JsonProperty("needVisionPings")]
  public int NeedVisionPings { get; set; }

  [JsonProperty("onMyWayPings")]
  public int OnMyWayPings { get; set; }
  [JsonProperty("pushPings")]
  public int PushPings { get; set; }

  [JsonProperty("retreatPings")]
  public int RetreatPings { get; set; }

  [JsonProperty("visionClearedPings")]
  public int VisionClearedPings { get; set; }

  // Champion stats
  [JsonProperty("baronKills")]
  public int BaronKills { get; set; }

  [JsonProperty("bountyLevel")]
  public int BountyLevel { get; set; }

  [JsonProperty("champExperience")]
  public int ChampExperience { get; set; }

  [JsonProperty("champLevel")]
  public int ChampLevel { get; set; }

  [JsonProperty("championTransform")]
  public int ChampionTransform { get; set; }

  [JsonProperty("consumablesPurchased")]
  public int ConsumablesPurchased { get; set; }

  // Damage stats
  [JsonProperty("damageDealtToBuildings")]
  public int DamageDealtToBuildings { get; set; }

  [JsonProperty("damageDealtToObjectives")]
  public int DamageDealtToObjectives { get; set; }

  [JsonProperty("damageDealtToTurrets")]
  public int DamageDealtToTurrets { get; set; }

  [JsonProperty("damageSelfMitigated")]
  public int DamageSelfMitigated { get; set; }

  [JsonProperty("magicDamageDealt")]
  public int MagicDamageDealt { get; set; }

  [JsonProperty("magicDamageDealtToChampions")]
  public int MagicDamageDealtToChampions { get; set; }

  [JsonProperty("magicDamageTaken")]
  public int MagicDamageTaken { get; set; }

  [JsonProperty("physicalDamageDealt")]
  public int PhysicalDamageDealt { get; set; }

  [JsonProperty("physicalDamageDealtToChampions")]
  public int PhysicalDamageDealtToChampions { get; set; }

  [JsonProperty("physicalDamageTaken")]
  public int PhysicalDamageTaken { get; set; }

  [JsonProperty("totalDamageDealt")]
  public int TotalDamageDealt { get; set; }

  [JsonProperty("totalDamageDealtToChampions")]
  public int TotalDamageDealtToChampions { get; set; }

  [JsonProperty("totalDamageTaken")]
  public int TotalDamageTaken { get; set; }

  [JsonProperty("trueDamageDealt")]
  public int TrueDamageDealt { get; set; }

  [JsonProperty("trueDamageDealtToChampions")]
  public int TrueDamageDealtToChampions { get; set; }

  [JsonProperty("trueDamageTaken")]
  public int TrueDamageTaken { get; set; }

  [JsonProperty("totalDamageShieldedOnTeammates")]
  public int TotalDamageShieldedOnTeammates { get; set; }

  // Vision and wards
  [JsonProperty("detectorWardsPlaced")]
  public int DetectorWardsPlaced { get; set; }

  [JsonProperty("sightWardsBoughtInGame")]
  public int SightWardsBoughtInGame { get; set; }

  [JsonProperty("visionScore")]
  public int VisionScore { get; set; }

  [JsonProperty("visionWardsBoughtInGame")]
  public int VisionWardsBoughtInGame { get; set; }

  [JsonProperty("wardsKilled")]
  public int WardsKilled { get; set; }

  [JsonProperty("wardsPlaced")]
  public int WardsPlaced { get; set; }

  // Kills and objectives
  [JsonProperty("doubleKills")]
  public int DoubleKills { get; set; }

  [JsonProperty("tripleKills")]
  public int TripleKills { get; set; }

  [JsonProperty("quadraKills")]
  public int QuadraKills { get; set; }

  [JsonProperty("pentaKills")]
  public int PentaKills { get; set; }

  [JsonProperty("unrealKills")]
  public int UnrealKills { get; set; }

  [JsonProperty("killingSprees")]
  public int KillingSprees { get; set; }

  [JsonProperty("largestKillingSpree")]
  public int LargestKillingSpree { get; set; }

  [JsonProperty("largestMultiKill")]
  public int LargestMultiKill { get; set; }

  [JsonProperty("dragonKills")]
  public int DragonKills { get; set; }

  [JsonProperty("inhibitorKills")]
  public int InhibitorKills { get; set; }

  [JsonProperty("inhibitorTakedowns")]
  public int InhibitorTakedowns { get; set; }

  [JsonProperty("inhibitorsLost")]
  public int InhibitorsLost { get; set; }

  [JsonProperty("nexusKills")]
  public int NexusKills { get; set; }

  [JsonProperty("nexusTakedowns")]
  public int NexusTakedowns { get; set; }

  [JsonProperty("nexusLost")]
  public int NexusLost { get; set; }

  [JsonProperty("objectivesStolen")]
  public int ObjectivesStolen { get; set; }

  [JsonProperty("objectivesStolenAssists")]
  public int ObjectivesStolenAssists { get; set; }

  [JsonProperty("turretKills")]
  public int TurretKills { get; set; }

  [JsonProperty("turretTakedowns")]
  public int TurretTakedowns { get; set; }

  [JsonProperty("turretsLost")]
  public int TurretsLost { get; set; }

  // Game state flags
  [JsonProperty("firstBloodAssist")]
  public bool FirstBloodAssist { get; set; }

  [JsonProperty("firstBloodKill")]
  public bool FirstBloodKill { get; set; }

  [JsonProperty("firstTowerAssist")]
  public bool FirstTowerAssist { get; set; }

  [JsonProperty("firstTowerKill")]
  public bool FirstTowerKill { get; set; }

  [JsonProperty("gameEndedInEarlySurrender")]
  public bool GameEndedInEarlySurrender { get; set; }

  [JsonProperty("gameEndedInSurrender")]
  public bool GameEndedInSurrender { get; set; }

  [JsonProperty("teamEarlySurrendered")]
  public bool TeamEarlySurrendered { get; set; }

  [JsonProperty("eligibleForProgression")]
  public bool EligibleForProgression { get; set; }

  // Gold and economy
  [JsonProperty("goldEarned")]
  public int GoldEarned { get; set; }

  [JsonProperty("goldSpent")]
  public int GoldSpent { get; set; }

  // Items
  [JsonProperty("item0")]
  public int Item0 { get; set; }

  [JsonProperty("item1")]
  public int Item1 { get; set; }

  [JsonProperty("item2")]
  public int Item2 { get; set; }

  [JsonProperty("item3")]
  public int Item3 { get; set; }

  [JsonProperty("item4")]
  public int Item4 { get; set; }

  [JsonProperty("item5")]
  public int Item5 { get; set; }

  [JsonProperty("item6")]
  public int Item6 { get; set; }

  [JsonProperty("itemsPurchased")]
  public int ItemsPurchased { get; set; }

  // Position and role
  [JsonProperty("individualPosition")]
  public string IndividualPosition { get; set; } = string.Empty;

  [JsonProperty("teamPosition")]
  public string TeamPosition { get; set; } = string.Empty;

  [JsonProperty("lane")]
  public string Lane { get; set; } = string.Empty;

  [JsonProperty("role")]
  public string Role { get; set; } = string.Empty;

  // Combat stats
  [JsonProperty("largestCriticalStrike")]
  public int LargestCriticalStrike { get; set; }

  [JsonProperty("longestTimeSpentLiving")]
  public int LongestTimeSpentLiving { get; set; }

  // Minions and jungle
  [JsonProperty("neutralMinionsKilled")]
  public int NeutralMinionsKilled { get; set; }

  [JsonProperty("totalMinionsKilled")]
  public int TotalMinionsKilled { get; set; }

  [JsonProperty("totalAllyJungleMinionsKilled")]
  public int TotalAllyJungleMinionsKilled { get; set; }

  [JsonProperty("totalEnemyJungleMinionsKilled")]
  public int TotalEnemyJungleMinionsKilled { get; set; }

  // Healing and support
  [JsonProperty("totalHeal")]
  public int TotalHeal { get; set; }

  [JsonProperty("totalHealsOnTeammates")]
  public int TotalHealsOnTeammates { get; set; }

  [JsonProperty("totalUnitsHealed")]
  public int TotalUnitsHealed { get; set; }

  // Time-based stats
  [JsonProperty("timeCCingOthers")]
  public int TimeCCingOthers { get; set; }

  [JsonProperty("timePlayed")]
  public int TimePlayed { get; set; }

  [JsonProperty("totalTimeCCDealt")]
  public int TotalTimeCCDealt { get; set; }

  [JsonProperty("totalTimeSpentDead")]
  public int TotalTimeSpentDead { get; set; }
  // Player scores (custom game modes)
  [JsonProperty("PlayerScore0")]
  public int PlayerScore0 { get; set; }

  [JsonProperty("PlayerScore1")]
  public int PlayerScore1 { get; set; }

  [JsonProperty("PlayerScore2")]
  public int PlayerScore2 { get; set; }

  [JsonProperty("PlayerScore3")]
  public int PlayerScore3 { get; set; }
  [JsonProperty("PlayerScore4")]
  public int PlayerScore4 { get; set; }

  [JsonProperty("PlayerScore5")]
  public int PlayerScore5 { get; set; }

  [JsonProperty("PlayerScore6")]
  public int PlayerScore6 { get; set; }

  [JsonProperty("PlayerScore7")]
  public int PlayerScore7 { get; set; }

  [JsonProperty("PlayerScore8")]
  public int PlayerScore8 { get; set; }

  [JsonProperty("PlayerScore9")]
  public int PlayerScore9 { get; set; }

  [JsonProperty("PlayerScore10")]
  public int PlayerScore10 { get; set; }

  [JsonProperty("PlayerScore11")]
  public int PlayerScore11 { get; set; }

  // Placement and augments
  [JsonProperty("placement")]
  public int Placement { get; set; }

  [JsonProperty("playerAugment1")]
  public int PlayerAugment1 { get; set; }

  [JsonProperty("playerAugment2")]
  public int PlayerAugment2 { get; set; }

  [JsonProperty("playerAugment3")]
  public int PlayerAugment3 { get; set; }
  [JsonProperty("playerAugment4")]
  public int PlayerAugment4 { get; set; }

  [JsonProperty("playerAugment5")]
  public int PlayerAugment5 { get; set; }

  [JsonProperty("playerAugment6")]
  public int PlayerAugment6 { get; set; }

  [JsonProperty("playerSubteamId")]
  public int PlayerSubteamId { get; set; }

  [JsonProperty("subteamPlacement")]
  public int SubteamPlacement { get; set; }

  // Summoner info
  [JsonProperty("profileIcon")]
  public int ProfileIcon { get; set; }

  [JsonProperty("riotIdGameName")]
  public string RiotIdGameName { get; set; } = string.Empty;

  [JsonProperty("riotIdTagline")]
  public string RiotIdTagline { get; set; } = string.Empty;

  [JsonProperty("summonerId")]
  public string SummonerId { get; set; } = string.Empty;

  [JsonProperty("summonerLevel")]
  public int SummonerLevel { get; set; }

  [JsonProperty("summonerName")]
  public string SummonerName { get; set; } = string.Empty;

  // Spell casts
  [JsonProperty("spell1Casts")]
  public int Spell1Casts { get; set; }

  [JsonProperty("spell2Casts")]
  public int Spell2Casts { get; set; }

  [JsonProperty("spell3Casts")]
  public int Spell3Casts { get; set; }

  [JsonProperty("spell4Casts")]
  public int Spell4Casts { get; set; }

  [JsonProperty("summoner1Casts")]
  public int Summoner1Casts { get; set; }

  [JsonProperty("summoner1Id")]
  public int Summoner1Id { get; set; }

  [JsonProperty("summoner2Casts")]
  public int Summoner2Casts { get; set; }

  [JsonProperty("summoner2Id")]
  public int Summoner2Id { get; set; }

  // Complex objects
  [JsonProperty("challenges")]
  public ChallengesDto? Challenges { get; set; }

  [JsonProperty("missions")]
  public MissionsDto? Missions { get; set; }

  [JsonProperty("perks")]
  public PerksDto? Perks { get; set; }
}

public class TeamDto
{
  [JsonProperty("teamId")]
  public int TeamId { get; set; }

  [JsonProperty("win")]
  public bool Win { get; set; }

  [JsonProperty("bans")]
  public List<BanDto> Bans { get; set; } = new();

  [JsonProperty("objectives")]
  public ObjectivesDto? Objectives { get; set; }

  [JsonProperty("feats")]
  public FeatsDto? Feats { get; set; }
}

public class BanDto
{
  [JsonProperty("championId")]
  public int ChampionId { get; set; }

  [JsonProperty("pickTurn")]
  public int PickTurn { get; set; }
}

public class ObjectivesDto
{
  [JsonProperty("baron")]
  public ObjectiveDto? Baron { get; set; }

  [JsonProperty("champion")]
  public ObjectiveDto? Champion { get; set; }

  [JsonProperty("dragon")]
  public ObjectiveDto? Dragon { get; set; }

  [JsonProperty("horde")]
  public ObjectiveDto? Horde { get; set; }

  [JsonProperty("inhibitor")]
  public ObjectiveDto? Inhibitor { get; set; }

  [JsonProperty("riftHerald")]
  public ObjectiveDto? RiftHerald { get; set; }

  [JsonProperty("tower")]
  public ObjectiveDto? Tower { get; set; }

  [JsonProperty("atakhan")]
  public ObjectiveDto? Atakhan { get; set; }
}

public class ObjectiveDto
{
  [JsonProperty("first")]
  public bool First { get; set; }

  [JsonProperty("kills")]
  public int Kills { get; set; }
}

public class FeatsDto
{
  [JsonProperty("EPIC_MONSTER_KILL")]
  public FeatDto? EpicMonsterKill { get; set; }

  [JsonProperty("FIRST_BLOOD")]
  public FeatDto? FirstBlood { get; set; }

  [JsonProperty("FIRST_TURRET")]
  public FeatDto? FirstTurret { get; set; }
}

public class FeatDto
{
  [JsonProperty("featState")]
  public int FeatState { get; set; }
}

// HTTP Request Models
public class MatchCollectionRequest
{
  /// <summary>
  /// Maximum number of matches to collect per unit (Region/Queue/Tier/Division).
  /// Valid range: 1-10000. Default: 100.
  /// </summary>
  [JsonProperty("maxMatchesPerUnit")]
  public int MaxMatchesPerUnit { get; set; }
}

// Complex DTOs for ParticipantDto
public class ChallengesDto
{
  [JsonProperty("12AssistStreakCount")]
  public int TwelveAssistStreakCount { get; set; }

  [JsonProperty("baronBuffGoldAdvantageOverThreshold")]
  public int BaronBuffGoldAdvantageOverThreshold { get; set; }
  [JsonProperty("controlWardTimeCoverageInRiverOrEnemyHalf")]
  public float ControlWardTimeCoverageInRiverOrEnemyHalf { get; set; }
  [JsonProperty("earliestBaron")]
  public float EarliestBaron { get; set; }

  [JsonProperty("earliestDragonTakedown")]
  public float EarliestDragonTakedown { get; set; }
  [JsonProperty("earliestElderDragon")]
  public float EarliestElderDragon { get; set; }

  [JsonProperty("earlyLaningPhaseGoldExpAdvantage")]
  public int EarlyLaningPhaseGoldExpAdvantage { get; set; }
  [JsonProperty("fasterSupportQuestCompletion")]
  public int FasterSupportQuestCompletion { get; set; }
  [JsonProperty("fastestLegendary")]
  public float FastestLegendary { get; set; }

  [JsonProperty("hadAfkTeammate")]
  public int HadAfkTeammate { get; set; }

  [JsonProperty("HealFromMapSources")]
  public float HealFromMapSources { get; set; }

  [JsonProperty("highestChampionDamage")]
  public int HighestChampionDamage { get; set; }

  [JsonProperty("highestCrowdControlScore")]
  public int HighestCrowdControlScore { get; set; }

  [JsonProperty("highestWardKills")]
  public int HighestWardKills { get; set; }

  [JsonProperty("junglerKillsEarlyJungle")]
  public int JunglerKillsEarlyJungle { get; set; }

  [JsonProperty("killsOnLanersEarlyJungleAsJungler")]
  public int KillsOnLanersEarlyJungleAsJungler { get; set; }

  [JsonProperty("laningPhaseGoldExpAdvantage")]
  public int LaningPhaseGoldExpAdvantage { get; set; }

  [JsonProperty("legendaryCount")]
  public int LegendaryCount { get; set; }

  [JsonProperty("maxCsAdvantageOnLaneOpponent")]
  public float MaxCsAdvantageOnLaneOpponent { get; set; }

  [JsonProperty("maxLevelLeadLaneOpponent")]
  public int MaxLevelLeadLaneOpponent { get; set; }

  [JsonProperty("mostWardsDestroyedOneSweeper")]
  public int MostWardsDestroyedOneSweeper { get; set; }

  [JsonProperty("mythicItemUsed")]
  public int MythicItemUsed { get; set; }

  [JsonProperty("playedChampSelectPosition")]
  public int PlayedChampSelectPosition { get; set; }

  [JsonProperty("soloTurretsLategame")]
  public int SoloTurretsLategame { get; set; }

  [JsonProperty("takedownsFirst25Minutes")]
  public int TakedownsFirst25Minutes { get; set; }

  [JsonProperty("teleportTakedowns")]
  public int TeleportTakedowns { get; set; }
  [JsonProperty("thirdInhibitorDestroyedTime")]
  public float ThirdInhibitorDestroyedTime { get; set; }

  [JsonProperty("threeWardsOneSweeperCount")]
  public int ThreeWardsOneSweeperCount { get; set; }

  [JsonProperty("visionScoreAdvantageLaneOpponent")]
  public float VisionScoreAdvantageLaneOpponent { get; set; }

  [JsonProperty("InfernalScalePickup")]
  public int InfernalScalePickup { get; set; }

  [JsonProperty("fistBumpParticipation")]
  public int FistBumpParticipation { get; set; }

  [JsonProperty("voidMonsterKill")]
  public int VoidMonsterKill { get; set; }

  [JsonProperty("abilityUses")]
  public int AbilityUses { get; set; }

  [JsonProperty("acesBefore15Minutes")]
  public int AcesBefore15Minutes { get; set; }

  [JsonProperty("alliedJungleMonsterKills")]
  public float AlliedJungleMonsterKills { get; set; }

  [JsonProperty("baronTakedowns")]
  public int BaronTakedowns { get; set; }
  [JsonProperty("blastConeOppositeOpponentCount")]
  public int BlastConeOppositeOpponentCount { get; set; }

  [JsonProperty("bountyGold")]
  public float BountyGold { get; set; }

  [JsonProperty("buffsStolen")]
  public int BuffsStolen { get; set; }

  [JsonProperty("completeSupportQuestInTime")]
  public int CompleteSupportQuestInTime { get; set; }

  [JsonProperty("controlWardsPlaced")]
  public int ControlWardsPlaced { get; set; }

  [JsonProperty("damagePerMinute")]
  public float DamagePerMinute { get; set; }

  [JsonProperty("damageTakenOnTeamPercentage")]
  public float DamageTakenOnTeamPercentage { get; set; }

  [JsonProperty("dancedWithRiftHerald")]
  public int DancedWithRiftHerald { get; set; }

  [JsonProperty("deathsByEnemyChamps")]
  public int DeathsByEnemyChamps { get; set; }

  [JsonProperty("dodgeSkillShotsSmallWindow")]
  public int DodgeSkillShotsSmallWindow { get; set; }

  [JsonProperty("doubleAces")]
  public int DoubleAces { get; set; }

  [JsonProperty("dragonTakedowns")]
  public int DragonTakedowns { get; set; }

  [JsonProperty("legendaryItemUsed")]
  public List<int> LegendaryItemUsed { get; set; } = new();

  [JsonProperty("effectiveHealAndShielding")]
  public float EffectiveHealAndShielding { get; set; }

  [JsonProperty("elderDragonKillsWithOpposingSoul")]
  public int ElderDragonKillsWithOpposingSoul { get; set; }

  [JsonProperty("elderDragonMultikills")]
  public int ElderDragonMultikills { get; set; }

  [JsonProperty("enemyChampionImmobilizations")]
  public int EnemyChampionImmobilizations { get; set; }

  [JsonProperty("enemyJungleMonsterKills")]
  public float EnemyJungleMonsterKills { get; set; }

  [JsonProperty("epicMonsterKillsNearEnemyJungler")]
  public int EpicMonsterKillsNearEnemyJungler { get; set; }

  [JsonProperty("epicMonsterKillsWithin30SecondsOfSpawn")]
  public int EpicMonsterKillsWithin30SecondsOfSpawn { get; set; }

  [JsonProperty("epicMonsterSteals")]
  public int EpicMonsterSteals { get; set; }

  [JsonProperty("epicMonsterStolenWithoutSmite")]
  public int EpicMonsterStolenWithoutSmite { get; set; }

  [JsonProperty("firstTurretKilled")]
  public int FirstTurretKilled { get; set; }

  [JsonProperty("firstTurretKilledTime")]
  public float FirstTurretKilledTime { get; set; }

  [JsonProperty("flawlessAces")]
  public int FlawlessAces { get; set; }

  [JsonProperty("fullTeamTakedown")]
  public int FullTeamTakedown { get; set; }

  [JsonProperty("gameLength")]
  public float GameLength { get; set; }

  [JsonProperty("getTakedownsInAllLanesEarlyJungleAsLaner")]
  public int GetTakedownsInAllLanesEarlyJungleAsLaner { get; set; }

  [JsonProperty("goldPerMinute")]
  public float GoldPerMinute { get; set; }

  [JsonProperty("hadOpenNexus")]
  public int HadOpenNexus { get; set; }

  [JsonProperty("immobilizeAndKillWithAlly")]
  public int ImmobilizeAndKillWithAlly { get; set; }

  [JsonProperty("initialBuffCount")]
  public int InitialBuffCount { get; set; }

  [JsonProperty("initialCrabCount")]
  public int InitialCrabCount { get; set; }

  [JsonProperty("jungleCsBefore10Minutes")]
  public float JungleCsBefore10Minutes { get; set; }

  [JsonProperty("junglerTakedownsNearDamagedEpicMonster")]
  public int JunglerTakedownsNearDamagedEpicMonster { get; set; }

  [JsonProperty("kda")]
  public float Kda { get; set; }

  [JsonProperty("killAfterHiddenWithAlly")]
  public int KillAfterHiddenWithAlly { get; set; }

  [JsonProperty("killedChampTookFullTeamDamageSurvived")]
  public int KilledChampTookFullTeamDamageSurvived { get; set; }

  [JsonProperty("killingSprees")]
  public int KillingSprees { get; set; }

  [JsonProperty("killParticipation")]
  public float KillParticipation { get; set; }

  [JsonProperty("killsNearEnemyTurret")]
  public int KillsNearEnemyTurret { get; set; }

  [JsonProperty("killsOnOtherLanesEarlyJungleAsLaner")]
  public int KillsOnOtherLanesEarlyJungleAsLaner { get; set; }

  [JsonProperty("killsOnRecentlyHealedByAramPack")]
  public int KillsOnRecentlyHealedByAramPack { get; set; }

  [JsonProperty("killsUnderOwnTurret")]
  public int KillsUnderOwnTurret { get; set; }

  [JsonProperty("killsWithHelpFromEpicMonster")]
  public int KillsWithHelpFromEpicMonster { get; set; }

  [JsonProperty("knockEnemyIntoTeamAndKill")]
  public int KnockEnemyIntoTeamAndKill { get; set; }

  [JsonProperty("kTurretsDestroyedBeforePlatesFall")]
  public int KTurretsDestroyedBeforePlatesFall { get; set; }

  [JsonProperty("landSkillShotsEarlyGame")]
  public int LandSkillShotsEarlyGame { get; set; }

  [JsonProperty("laneMinionsFirst10Minutes")]
  public int LaneMinionsFirst10Minutes { get; set; }

  [JsonProperty("lostAnInhibitor")]
  public int LostAnInhibitor { get; set; }

  [JsonProperty("maxKillDeficit")]
  public int MaxKillDeficit { get; set; }

  [JsonProperty("mejaisFullStackInTime")]
  public int MejaisFullStackInTime { get; set; }

  [JsonProperty("moreEnemyJungleThanOpponent")]
  public float MoreEnemyJungleThanOpponent { get; set; }

  [JsonProperty("multiKillOneSpell")]
  public int MultiKillOneSpell { get; set; }

  [JsonProperty("multikills")]
  public int Multikills { get; set; }

  [JsonProperty("multikillsAfterAggressiveFlash")]
  public int MultikillsAfterAggressiveFlash { get; set; }

  [JsonProperty("multiTurretRiftHeraldCount")]
  public int MultiTurretRiftHeraldCount { get; set; }

  [JsonProperty("outerTurretExecutesBefore10Minutes")]
  public int OuterTurretExecutesBefore10Minutes { get; set; }

  [JsonProperty("outnumberedKills")]
  public int OutnumberedKills { get; set; }

  [JsonProperty("outnumberedNexusKill")]
  public int OutnumberedNexusKill { get; set; }

  [JsonProperty("perfectDragonSoulsTaken")]
  public int PerfectDragonSoulsTaken { get; set; }

  [JsonProperty("perfectGame")]
  public int PerfectGame { get; set; }

  [JsonProperty("pickKillWithAlly")]
  public int PickKillWithAlly { get; set; }

  [JsonProperty("poroExplosions")]
  public int PoroExplosions { get; set; }

  [JsonProperty("quickCleanse")]
  public int QuickCleanse { get; set; }

  [JsonProperty("quickFirstTurret")]
  public int QuickFirstTurret { get; set; }

  [JsonProperty("quickSoloKills")]
  public int QuickSoloKills { get; set; }

  [JsonProperty("riftHeraldTakedowns")]
  public int RiftHeraldTakedowns { get; set; }

  [JsonProperty("saveAllyFromDeath")]
  public int SaveAllyFromDeath { get; set; }

  [JsonProperty("scuttleCrabKills")]
  public int ScuttleCrabKills { get; set; }

  [JsonProperty("shortestTimeToAceFromFirstTakedown")]
  public float ShortestTimeToAceFromFirstTakedown { get; set; }

  [JsonProperty("skillshotsDodged")]
  public int SkillshotsDodged { get; set; }

  [JsonProperty("skillshotsHit")]
  public int SkillshotsHit { get; set; }

  [JsonProperty("snowballsHit")]
  public int SnowballsHit { get; set; }

  [JsonProperty("soloBaronKills")]
  public int SoloBaronKills { get; set; }

  [JsonProperty("SWARM_DefeatAatrox")]
  public int SwarmDefeatAatrox { get; set; }

  [JsonProperty("SWARM_DefeatBriar")]
  public int SwarmDefeatBriar { get; set; }

  [JsonProperty("SWARM_DefeatMiniBosses")]
  public int SwarmDefeatMiniBosses { get; set; }

  [JsonProperty("SWARM_EvolveWeapon")]
  public int SwarmEvolveWeapon { get; set; }

  [JsonProperty("SWARM_Have3Passives")]
  public int SwarmHave3Passives { get; set; }

  [JsonProperty("SWARM_KillEnemy")]
  public int SwarmKillEnemy { get; set; }

  [JsonProperty("SWARM_PickupGold")]
  public float SwarmPickupGold { get; set; }

  [JsonProperty("SWARM_ReachLevel50")]
  public int SwarmReachLevel50 { get; set; }

  [JsonProperty("SWARM_Survive15Min")]
  public int SwarmSurvive15Min { get; set; }

  [JsonProperty("SWARM_WinWith5EvolvedWeapons")]
  public int SwarmWinWith5EvolvedWeapons { get; set; }

  [JsonProperty("soloKills")]
  public int SoloKills { get; set; }

  [JsonProperty("stealthWardsPlaced")]
  public int StealthWardsPlaced { get; set; }

  [JsonProperty("survivedSingleDigitHpCount")]
  public int SurvivedSingleDigitHpCount { get; set; }

  [JsonProperty("survivedThreeImmobilizesInFight")]
  public int SurvivedThreeImmobilizesInFight { get; set; }

  [JsonProperty("takedownOnFirstTurret")]
  public int TakedownOnFirstTurret { get; set; }

  [JsonProperty("takedowns")]
  public int Takedowns { get; set; }

  [JsonProperty("takedownsAfterGainingLevelAdvantage")]
  public int TakedownsAfterGainingLevelAdvantage { get; set; }

  [JsonProperty("takedownsBeforeJungleMinionSpawn")]
  public int TakedownsBeforeJungleMinionSpawn { get; set; }

  [JsonProperty("takedownsFirstXMinutes")]
  public int TakedownsFirstXMinutes { get; set; }

  [JsonProperty("takedownsInAlcove")]
  public int TakedownsInAlcove { get; set; }

  [JsonProperty("takedownsInEnemyFountain")]
  public int TakedownsInEnemyFountain { get; set; }

  [JsonProperty("teamBaronKills")]
  public int TeamBaronKills { get; set; }

  [JsonProperty("teamDamagePercentage")]
  public float TeamDamagePercentage { get; set; }

  [JsonProperty("teamElderDragonKills")]
  public int TeamElderDragonKills { get; set; }

  [JsonProperty("teamRiftHeraldKills")]
  public int TeamRiftHeraldKills { get; set; }

  [JsonProperty("tookLargeDamageSurvived")]
  public int TookLargeDamageSurvived { get; set; }

  [JsonProperty("turretPlatesTaken")]
  public int TurretPlatesTaken { get; set; }

  [JsonProperty("turretsTakenWithRiftHerald")]
  public int TurretsTakenWithRiftHerald { get; set; }

  [JsonProperty("turretTakedowns")]
  public int TurretTakedowns { get; set; }

  [JsonProperty("twentyMinionsIn3SecondsCount")]
  public int TwentyMinionsIn3SecondsCount { get; set; }

  [JsonProperty("twoWardsOneSweeperCount")]
  public int TwoWardsOneSweeperCount { get; set; }

  [JsonProperty("unseenRecalls")]
  public int UnseenRecalls { get; set; }

  [JsonProperty("visionScorePerMinute")]
  public float VisionScorePerMinute { get; set; }

  [JsonProperty("wardsGuarded")]
  public int WardsGuarded { get; set; }

  [JsonProperty("wardTakedowns")]
  public int WardTakedowns { get; set; }

  [JsonProperty("wardTakedownsBefore20M")]
  public int WardTakedownsBefore20M { get; set; }
}

public class MissionsDto
{
  [JsonProperty("playerScore0")]
  public int PlayerScore0 { get; set; }

  [JsonProperty("playerScore1")]
  public int PlayerScore1 { get; set; }

  [JsonProperty("playerScore2")]
  public int PlayerScore2 { get; set; }

  [JsonProperty("playerScore3")]
  public int PlayerScore3 { get; set; }

  [JsonProperty("playerScore4")]
  public int PlayerScore4 { get; set; }

  [JsonProperty("playerScore5")]
  public int PlayerScore5 { get; set; }

  [JsonProperty("playerScore6")]
  public int PlayerScore6 { get; set; }

  [JsonProperty("playerScore7")]
  public int PlayerScore7 { get; set; }

  [JsonProperty("playerScore8")]
  public int PlayerScore8 { get; set; }

  [JsonProperty("playerScore9")]
  public int PlayerScore9 { get; set; }

  [JsonProperty("playerScore10")]
  public int PlayerScore10 { get; set; }

  [JsonProperty("playerScore11")]
  public int PlayerScore11 { get; set; }
}

public class PerksDto
{
  [JsonProperty("statPerks")]
  public PerkStatsDto StatPerks { get; set; } = new();

  [JsonProperty("styles")]
  public List<PerkStyleDto> Styles { get; set; } = new();
}

public class PerkStatsDto
{
  [JsonProperty("defense")]
  public int Defense { get; set; }

  [JsonProperty("flex")]
  public int Flex { get; set; }

  [JsonProperty("offense")]
  public int Offense { get; set; }
}

public class PerkStyleDto
{
  [JsonProperty("description")]
  public string Description { get; set; } = string.Empty;

  [JsonProperty("selections")]
  public List<PerkStyleSelectionDto> Selections { get; set; } = new();

  [JsonProperty("style")]
  public int Style { get; set; }
}

public class PerkStyleSelectionDto
{
  [JsonProperty("perk")]
  public int Perk { get; set; }

  [JsonProperty("var1")]
  public int Var1 { get; set; }

  [JsonProperty("var2")]
  public int Var2 { get; set; }

  [JsonProperty("var3")]
  public int Var3 { get; set; }
}
