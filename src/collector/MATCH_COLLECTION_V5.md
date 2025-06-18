# Match Collection Durable Function

This document describes the new match collection durable function that uses the League of Legends Match-v5 API to collect match data for ranked players.

## Overview

The match collection system consists of two main phases:
1. **Match ID Collection**: Collects match IDs for all ranked players across different regional domains
2. **Match Details Processing**: Fetches full match details for the collected match IDs

## Components

### MatchCollectionOrchestrator

The main orchestrator function that coordinates the entire match collection process.

**Function Name**: `MatchCollectionOrchestrator`

**Workflow**:
1. Retrieves the last collection start time from blob storage
2. Calculates the end time as current DateTime
3. Gets all ranked PUUIDs from the player_stats database
4. Groups PUUIDs by regional domain (americas, asia, europe, sea)
5. Runs parallel activities to collect match IDs for each domain
6. Runs parallel activities to process match details for each domain
7. Updates the start time configuration for the next run

### MatchCollectionActivities

Contains the activity functions that perform the actual API calls and data processing.

#### CollectMatchesForDomainActivity

Collects match IDs for all players in a specific regional domain.

**Parameters**:
- `MatchCollectionState`: Contains domain, time range, and list of PUUIDs

**Process**:
- For each PUUID, calls the match-v5 API to get match IDs
- Handles pagination by incrementing the start parameter
- Respects rate limits (stops if wait time > 1 minute)
- Has a maximum runtime of 30 minutes per activity
- Creates `MatchDocument` entries with `processed = false`
- Batch upserts matches to Cosmos DB, partitioned by region

#### ProcessMatchDetailsActivity

Processes unprocessed matches to fetch full match details.

**Parameters**:
- `domain`: The regional domain to process

**Process**:
- Gets unprocessed matches for each region in the domain
- Calls the match-v5 API to get full match details
- Updates match documents with the full data and sets `processed = true`
- Handles rate limits and time constraints

## Configuration

### Environment Variables

- `RIOT_API_KEY`: Your Riot API key
- `AZURE_COSMOS_ENDPOINT`: Cosmos DB endpoint
- `AZURE_COSMOS_KEY`: Cosmos DB key
- `AZURE_STORAGE_CONNECTION_STRING`: Azure Storage connection string for blob storage
- `MINIMUM_TIER`: Minimum tier for players (default: "IRON")
- `MINIMUM_DIVISION`: Minimum division for players (default: "V")

### Blob Storage Configuration

The system uses blob storage to persist the collection start time:
- **Container**: `match-collection-config`
- **Blob**: `match-collection-start-time.json`
- **Format**: JSON with `start_time` field

### Database Schema

#### Matches Container

**Container Name**: `matches`
**Partition Key**: `/region`

**Document Structure**:
```json
{
  "id": "NA1_1234567890",
  "matchId": "NA1_1234567890",
  "region": "NA1",
  "processed": false,
  "created_at": "2024-01-01T00:00:00Z",
  "match_data": { /* Full match object when processed */ }
}
```

## Regional Domain Mapping

The system maps individual regions to their corresponding domains for the Match-v5 API:

- **Americas**: NA1, BR1, LA1, LA2
- **Asia**: KR, JP1
- **Europe**: EUW1, EUN1, ME1, TR1, RU
- **SEA**: OC1, SG2, TW2, VN2

## Rate Limiting

The system implements intelligent rate limiting:
- Monitors `X-App-Rate-Limit` headers from the API
- Stops processing if wait time exceeds 1 minute
- Each activity has a maximum runtime of 30 minutes
- Uses separate activity instances per domain to isolate rate limits

## API Endpoints

### Start Match Collection

**POST** `/api/StartMatchCollection`

Starts a new match collection orchestration.

**Response**:
```json
{
  "message": "Match Collection started successfully",
  "instanceId": "12345678-1234-1234-1234-123456789012",
  "statusQueryGetUri": "https://your-function-app.azurewebsites.net/api/status/12345678-1234-1234-1234-123456789012"
}
```

### Get Orchestration Status

**GET** `/api/status/{instanceId}`

Gets the status of a running orchestration.

## Match Types

The system only collects ranked matches (`match_type = "ranked"`).

## Deployment Notes

1. Ensure all environment variables are set
2. The Cosmos DB database and containers will be created automatically
3. The blob storage container will be created automatically
4. The system requires proper permissions for Cosmos DB and Azure Storage

## Monitoring

The system provides detailed logging at various levels:
- Orchestrator progress and completion status
- Activity start/stop times and counts
- Rate limit encounters and wait times
- Error conditions and retry logic

Monitor the Application Insights logs for operational visibility.
