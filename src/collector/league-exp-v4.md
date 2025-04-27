# League-exp-v4 API Reference

## Rate Limits
- **GET /lol/league-exp/v4/entries/{queue}/{tier}/{division}**: 50 requests every 10 seconds

## Important Notes
- This API provides expanded league data beyond the standard league-v4 endpoints
- Results are paginated - use the `page` parameter to retrieve additional entries
- Rate limits are separate from league-v4 endpoints
- Queue types are case-sensitive and must match exactly
- All tiers except Challenger, Grandmaster, and Master have divisions I-IV

## Endpoint

### GET /lol/league-exp/v4/entries/{queue}/{tier}/{division}
Get all the league entries.

**Path Parameters:**
- `queue` (string, required): Queue type
  - `RANKED_SOLO_5x5`
  - `RANKED_TFT`
  - `RANKED_FLEX_SR`
  - `RANKED_FLEX_TT`
- `tier` (string, required): Tier level
  - `CHALLENGER`
  - `GRANDMASTER`
  - `MASTER`
  - `DIAMOND`
  - `EMERALD`
  - `PLATINUM`
  - `GOLD`
  - `SILVER`
  - `BRONZE`
  - `IRON`
- `division` (string, required): Division within tier
  - `I`
  - `II`
  - `III`
  - `IV`

**Query Parameters:**
- `page` (int, optional): Page number, defaults to 1
  - Used for pagination through large result sets
  - Each page typically contains up to 205 entries
  - Continue requesting subsequent pages until an empty array is returned
- `api_key` (string, required): Your Riot API key

**Response:** `Set[LeagueEntryDTO]` - Array of league entry objects

## Example Usage

### Basic Request
```
GET https://na1.api.riotgames.com/lol/league-exp/v4/entries/RANKED_SOLO_5x5/GOLD/I?page=1&api_key=YOUR_API_KEY
```

### Pagination Example
```
# First page
GET https://br1.api.riotgames.com/lol/league-exp/v4/entries/RANKED_SOLO_5x5/GOLD/I?page=1&api_key=YOUR_API_KEY

# Second page
GET https://br1.api.riotgames.com/lol/league-exp/v4/entries/RANKED_SOLO_5x5/GOLD/I?page=2&api_key=YOUR_API_KEY

# Continue until empty array is returned
```

### Different Queue Types
```
# Solo/Duo Ranked
GET https://na1.api.riotgames.com/lol/league-exp/v4/entries/RANKED_SOLO_5x5/DIAMOND/II?page=1&api_key=YOUR_API_KEY

# Flex 5v5
GET https://na1.api.riotgames.com/lol/league-exp/v4/entries/RANKED_FLEX_SR/SILVER/IV?page=1&api_key=YOUR_API_KEY
br`

## Data Structures

### LeagueEntryDTO
```
leagueId: string
summonerId: string - Player's summonerId (Encrypted)
puuid: string - Player's encrypted puuid
queueType: string
tier: string
rank: string - The player's division within a tier
leaguePoints: int
wins: int - Winning team on Summoners Rift. First placement in Teamfight Tactics
losses: int - Losing team on Summoners Rift. Second through eighth placement in Teamfight Tactics
hotStreak: boolean
veteran: boolean
freshBlood: boolean
inactive: boolean
miniSeries: MiniSeriesDTO
```

### MiniSeriesDTO
```
losses: int
progress: string
target: int
wins: int
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

## Regions
- BR1
- EUN1
- EUW1
- JP1
- KR
- LA1
- LA2
- ME1
- NA1
- OC1
- RU
- SG2
- TR1
- TW2
- VN2
