# Match-v5 API Reference

## Base URLs
- **AMERICAS**: `https://americas.api.riotgames.com`
- **ASIA**: `https://asia.api.riotgames.com`
- **EUROPE**: `https://europe.api.riotgames.com`
- **SEA**: `https://sea.api.riotgames.com`

## Authentication
All requests require an API key passed as a query parameter:
- `api_key=YOUR_API_KEY`

## Rate Limits
- **GET /lol/match/v5/matches/{matchId}**: 2000 requests every 10 seconds
- **GET /lol/match/v5/matches/by-puuid/{puuid}/ids**: 2000 requests every 10 seconds  
- **GET /lol/match/v5/matches/{matchId}/timeline**: 2000 requests every 10 seconds

## Routing Information
- **AMERICAS**: NA, BR, LAN, LAS
- **ASIA**: KR, JP  
- **EUROPE**: EUNE, EUW, ME1, TR, RU
- **SEA**: OCE, SG2, TW2, VN2

## Important Notes
- Match data is available for matches played after June 16, 2021
- Match IDs are region-specific and must be used with the correct regional routing
- Timeline data provides detailed frame-by-frame information about match events
- Use PUUID for cross-region player identification
- Rate limits are shared across all match-v5 endpoints

## Endpoints

### GET /lol/match/v5/matches/by-puuid/{puuid}/ids
Get match IDs by player PUUID.

**Path Parameters:**
- `puuid` (string, required): Player PUUID

**Query Parameters:**
- `startTime` (long, optional): Epoch timestamp in seconds. Matches before June 16, 2021 excluded if set
- `endTime` (long, optional): Epoch timestamp in seconds
- `queue` (int, optional): Queue ID filter
- `type` (string, optional): Match type filter - `ranked`, `normal`, `tourney`, `tutorial`
- `start` (int, optional): Start index, default 0
- `count` (int, optional): Number of matches to return, default 20, max 100

**Response:** `List[string]` - Array of match IDs

**Example Request:**
```
https://americas.api.riotgames.com/lol/match/v5/matches/by-puuid/abc123def456/ids?api_key=YOUR_API_KEY
```

---

### GET /lol/match/v5/matches/{matchId}
Get match details by match ID.

**Path Parameters:**
- `matchId` (string, required): Match ID

**Response:** `MatchDto`

**Example Request:**
```
https://americas.api.riotgames.com/lol/match/v5/matches/NA1_5272242490?api_key=YOUR_API_KEY
```

---

### GET /lol/match/v5/matches/{matchId}/timeline
Get match timeline by match ID.

**Path Parameters:**
- `matchId` (string, required): Match ID

**Response:** `TimelineDto`

**Example Request:**
```
https://americas.api.riotgames.com/lol/match/v5/matches/NA1_5272242490/timeline?api_key=YOUR_API_KEY
```

## Data Structures

### MatchDto
```
metadata: MetadataDto
info: InfoDto
```

### MetadataDto
```
dataVersion: string
matchId: string
participants: List[string] - PUUIDs
```

### InfoDto
```
endOfGameResult: string - Game termination indicator
gameCreation: long - Game creation timestamp
gameDuration: long - Duration in milliseconds (pre-11.20) or seconds (post-11.20)
gameEndTimestamp: long - Game end timestamp (added patch 11.20)
gameId: long
gameMode: string
gameName: string
gameStartTimestamp: long
gameType: string
gameVersion: string - First two parts indicate patch
mapId: int
participants: List[ParticipantDto]
platformId: string
queueId: int
teams: List[TeamDto]
tournamentCode: string - Added patch 11.13
```

### ParticipantDto
```
allInPings: int - Yellow crossed swords
assistMePings: int - Green flag
assists: int
baronKills: int
bountyLevel: int
champExperience: int
champLevel: int
championId: int - Invalid pre-patch 11.4, use championName
championName: string
commandPings: int - Blue generic ping
championTransform: int - Kayn transformations (0=None, 1=Slayer, 2=Assassin)
consumablesPurchased: int
challenges: ChallengesDto
damageDealtToBuildings: int
damageDealtToObjectives: int
damageDealtToTurrets: int
damageSelfMitigated: int
deaths: int
detectorWardsPlaced: int
doubleKills: int
dragonKills: int
eligibleForProgression: boolean
enemyMissingPings: int - Yellow questionmark
enemyVisionPings: int - Red eyeball
firstBloodAssist: boolean
firstBloodKill: boolean
firstTowerAssist: boolean
firstTowerKill: boolean
gameEndedInEarlySurrender: boolean
gameEndedInSurrender: boolean
holdPings: int
getBackPings: int - Yellow circle with horizontal line
goldEarned: int
goldSpent: int
individualPosition: string - Individual position estimate
inhibitorKills: int
inhibitorTakedowns: int
inhibitorsLost: int
item0-6: int - Item slots
itemsPurchased: int
killingSprees: int
kills: int
lane: string
largestCriticalStrike: int
largestKillingSpree: int
largestMultiKill: int
longestTimeSpentLiving: int
magicDamageDealt: int
magicDamageDealtToChampions: int
magicDamageTaken: int
missions: MissionsDto
neutralMinionsKilled: int - Jungle monsters and pets
needVisionPings: int - Green ward
nexusKills: int
nexusTakedowns: int
nexusLost: int
objectivesStolen: int
objectivesStolenAssists: int
onMyWayPings: int - Blue arrow
participantId: int
playerScore0-11: int - Custom game mode scores
pentaKills: int
perks: PerksDto
physicalDamageDealt: int
physicalDamageDealtToChampions: int
physicalDamageTaken: int
placement: int
playerAugment1-4: int
playerSubteamId: int
pushPings: int - Green minion
profileIcon: int
puuid: string
quadraKills: int
riotIdGameName: string
riotIdTagline: string
role: string
sightWardsBoughtInGame: int
spell1-4Casts: int
subteamPlacement: int
summoner1-2Casts: int
summoner1-2Id: int
summonerId: string
summonerLevel: int
summonerName: string
teamEarlySurrendered: boolean
teamId: int
teamPosition: string - Team position estimate (recommended over individualPosition)
timeCCingOthers: int
timePlayed: int
totalAllyJungleMinionsKilled: int
totalDamageDealt: int
totalDamageDealtToChampions: int
totalDamageShieldedOnTeammates: int
totalDamageTaken: int
totalEnemyJungleMinionsKilled: int
totalHeal: int - All healing applied
totalHealsOnTeammates: int - Effective healing on teammates
totalMinionsKilled: int - Lane minions only
totalTimeCCDealt: int
totalTimeSpentDead: int
totalUnitsHealed: int
tripleKills: int
trueDamageDealt: int
trueDamageDealtToChampions: int
trueDamageTaken: int
turretKills: int
turretTakedowns: int
turretsLost: int
unrealKills: int
visionScore: int
visionClearedPings: int
visionWardsBoughtInGame: int
wardsKilled: int
wardsPlaced: int
win: boolean
```

### ChallengesDto
```
12AssistStreakCount: int
baronBuffGoldAdvantageOverThreshold: int
controlWardTimeCoverageInRiverOrEnemyHalf: float
earliestBaron: int
earliestDragonTakedown: int
earliestElderDragon: int
earlyLaningPhaseGoldExpAdvantage: int
fasterSupportQuestCompletion: int
fastestLegendary: int
hadAfkTeammate: int
highestChampionDamage: int
highestCrowdControlScore: int
highestWardKills: int
junglerKillsEarlyJungle: int
killsOnLanersEarlyJungleAsJungler: int
laningPhaseGoldExpAdvantage: int
legendaryCount: int
maxCsAdvantageOnLaneOpponent: float
maxLevelLeadLaneOpponent: int
mostWardsDestroyedOneSweeper: int
mythicItemUsed: int
playedChampSelectPosition: int
soloTurretsLategame: int
takedownsFirst25Minutes: int
teleportTakedowns: int
thirdInhibitorDestroyedTime: int
threeWardsOneSweeperCount: int
visionScoreAdvantageLaneOpponent: float
InfernalScalePickup: int
fistBumpParticipation: int
voidMonsterKill: int
abilityUses: int
acesBefore15Minutes: int
alliedJungleMonsterKills: float
baronTakedowns: int
blastConeOppositeOpponentCount: int
bountyGold: int
buffsStolen: int
completeSupportQuestInTime: int
controlWardsPlaced: int
damagePerMinute: float
damageTakenOnTeamPercentage: float
dancedWithRiftHerald: int
deathsByEnemyChamps: int
dodgeSkillShotsSmallWindow: int
doubleAces: int
dragonTakedowns: int
legendaryItemUsed: List[int]
effectiveHealAndShielding: float
elderDragonKillsWithOpposingSoul: int
elderDragonMultikills: int
enemyChampionImmobilizations: int
enemyJungleMonsterKills: float
epicMonsterKillsNearEnemyJungler: int
epicMonsterKillsWithin30SecondsOfSpawn: int
epicMonsterSteals: int
epicMonsterStolenWithoutSmite: int
firstTurretKilled: int
firstTurretKilledTime: float
flawlessAces: int
fullTeamTakedown: int
gameLength: float
getTakedownsInAllLanesEarlyJungleAsLaner: int
goldPerMinute: float
hadOpenNexus: int
immobilizeAndKillWithAlly: int
initialBuffCount: int
initialCrabCount: int
jungleCsBefore10Minutes: float
junglerTakedownsNearDamagedEpicMonster: int
kda: float
killAfterHiddenWithAlly: int
killedChampTookFullTeamDamageSurvived: int
killingSprees: int
killParticipation: float
killsNearEnemyTurret: int
killsOnOtherLanesEarlyJungleAsLaner: int
killsOnRecentlyHealedByAramPack: int
killsUnderOwnTurret: int
killsWithHelpFromEpicMonster: int
knockEnemyIntoTeamAndKill: int
kTurretsDestroyedBeforePlatesFall: int
landSkillShotsEarlyGame: int
laneMinionsFirst10Minutes: int
lostAnInhibitor: int
maxKillDeficit: int
mejaisFullStackInTime: int
moreEnemyJungleThanOpponent: float
multiKillOneSpell: int - OneStone challenge variant
multikills: int
multikillsAfterAggressiveFlash: int
multiTurretRiftHeraldCount: int
outerTurretExecutesBefore10Minutes: int
outnumberedKills: int
outnumberedNexusKill: int
perfectDragonSoulsTaken: int
perfectGame: int
pickKillWithAlly: int
poroExplosions: int
quickCleanse: int
quickFirstTurret: int
quickSoloKills: int
riftHeraldTakedowns: int
saveAllyFromDeath: int
scuttleCrabKills: int
shortestTimeToAceFromFirstTakedown: float
skillshotsDodged: int
skillshotsHit: int
snowballsHit: int
soloBaronKills: int
SWARM_DefeatAatrox: int
SWARM_DefeatBriar: int
SWARM_DefeatMiniBosses: int
SWARM_EvolveWeapon: int
SWARM_Have3Passives: int
SWARM_KillEnemy: int
SWARM_PickupGold: float
SWARM_ReachLevel50: int
SWARM_Survive15Min: int
SWARM_WinWith5EvolvedWeapons: int
soloKills: int
stealthWardsPlaced: int
survivedSingleDigitHpCount: int
survivedThreeImmobilizesInFight: int
takedownOnFirstTurret: int
takedowns: int
takedownsAfterGainingLevelAdvantage: int
takedownsBeforeJungleMinionSpawn: int
takedownsFirstXMinutes: int
takedownsInAlcove: int
takedownsInEnemyFountain: int
teamBaronKills: int
teamDamagePercentage: float
teamElderDragonKills: int
teamRiftHeraldKills: int
tookLargeDamageSurvived: int
turretPlatesTaken: int
turretsTakenWithRiftHerald: int - 30s window after herald charge
turretTakedowns: int
twentyMinionsIn3SecondsCount: int
twoWardsOneSweeperCount: int
unseenRecalls: int
visionScorePerMinute: float
wardsGuarded: int
wardTakedowns: int
wardTakedownsBefore20M: int
```

### MissionsDto
```
playerScore0-11: int
```

### PerksDto
```
statPerks: PerkStatsDto
styles: List[PerkStyleDto]
```

### PerkStatsDto
```
defense: int
flex: int
offense: int
```

### PerkStyleDto
```
description: string
selections: List[PerkStyleSelectionDto]
style: int
```

### PerkStyleSelectionDto
```
perk: int
var1: int
var2: int
var3: int
```

### TeamDto
```
bans: List[BanDto]
objectives: ObjectivesDto
teamId: int
win: boolean
```

### BanDto
```
championId: int
pickTurn: int
```

### ObjectivesDto
```
baron: ObjectiveDto
champion: ObjectiveDto
dragon: ObjectiveDto
horde: ObjectiveDto
inhibitor: ObjectiveDto
riftHerald: ObjectiveDto
tower: ObjectiveDto
```

### ObjectiveDto
```
first: boolean
kills: int
```

## Timeline Data Structures

### TimelineDto
```
metadata: MetadataTimeLineDto
info: InfoTimeLineDto
```

### MetadataTimeLineDto
```
dataVersion: string
matchId: string
participants: List[string] - PUUIDs
```

### InfoTimeLineDto
```
endOfGameResult: string
frameInterval: long
gameId: long
participants: List[ParticipantTimeLineDto]
frames: List[FramesTimeLineDto]
```

### ParticipantTimeLineDto
```
participantId: int
puuid: string
```

### FramesTimeLineDto
```
events: List[EventsTimeLineDto]
participantFrames: ParticipantFramesDto
timestamp: int
```

### EventsTimeLineDto
```
timestamp: long
realTimestamp: long
type: string
```

### ParticipantFramesDto
```
1-9: ParticipantFrameDto - Key-value mapping for each participant
```

### ParticipantFrameDto
```
championStats: ChampionStatsDto
currentGold: int
damageStats: DamageStatsDto
goldPerSecond: int
jungleMinionsKilled: int
level: int
minionsKilled: int
participantId: int
position: PositionDto
timeEnemySpentControlled: int
totalGold: int
xp: int
```

### ChampionStatsDto
```
abilityHaste: int
abilityPower: int
armor: int
armorPen: int
armorPenPercent: int
attackDamage: int
attackSpeed: int
bonusArmorPenPercent: int
bonusMagicPenPercent: int
ccReduction: int
cooldownReduction: int
health: int
healthMax: int
healthRegen: int
lifesteal: int
magicPen: int
magicPenPercent: int
magicResist: int
movementSpeed: int
omnivamp: int
physicalVamp: int
power: int
powerMax: int
powerRegen: int
spellVamp: int
```

### DamageStatsDto
```
magicDamageDone: int
magicDamageDoneToChampions: int
magicDamageTaken: int
physicalDamageDone: int
physicalDamageDoneToChampions: int
physicalDamageTaken: int
totalDamageDone: int
totalDamageDoneToChampions: int
totalDamageTaken: int
trueDamageDone: int
trueDamageDoneToChampions: int
trueDamageTaken: int
```

### PositionDto
```
x: int
y: int
```

## Error Codes
- 400: Bad request
- 401: Unauthorized
- 403: Forbidden
- 404: Data not found
- 405: Method not allowed
- 415: Unsupported media type
- 429: Rate limit exceeded
- 500: Internal server error
- 502: Bad gateway
- 503: Service unavailable
- 504: Gateway timeout
