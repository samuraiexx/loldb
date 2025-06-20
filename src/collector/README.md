# League of Legends Data Collector

A durable Azure Function that collects League of Legends player data using the Riot Games API and stores it in Azure Cosmos DB.

## Overview

This application implements a durable function orchestrator that:
- Processes all regions, queue types, tiers, and divisions (~1,860 processing states)
- Respects rate limits (20 requests/second, 100 requests/2 minutes per region)  
- Saves player snapshots to Azure Cosmos DB
- Tracks processing progress and handles resumption
- Implements comprehensive error handling and logging

## Project Structure

```
collector/
├── Functions/
│   ├── PlayerStatusCollectionOrchestrator.cs      # Main orchestrator function
│   └── PlayerStatusCollectionActivity.cs       # Region processing activity
├── Models/
│   └── LeagueModels.cs                # Data models and DTOs
├── Services/
│   ├── CosmosDbService.cs             # Cosmos DB operations
│   └── RiotApiService.cs              # Riot API client
├── Tests/
│   ├── RiotApiServiceTests.cs         # API service tests
│   ├── CosmosDbServiceTests.cs        # Database service tests
│   └── PlayerStatusCollectionActivityTests.cs  # Activity function tests
├── Validation/
│   └── ValidationScript.cs            # Validation and checks
├── MyHttpTrigger.cs                   # HTTP endpoints
├── Program.cs                         # Service configuration
├── host.json                          # Function app configuration
├── local.settings.json                # Local environment variables
├── collector.csproj                   # Project dependencies
├── README.md                          # This file
└── INTEGRATION_TEST.md                # Testing instructions
```

## Architecture

### Functions
- **PlayerStatusCollectionOrchestrator**: Main orchestrator that coordinates the data collection process
- **PlayerStatusCollectionActivity**: Activity function that handles data collection for a specific region
- **StartPlayerStatusCollection**: HTTP trigger to start the orchestration
- **GetOrchestrationStatus**: HTTP trigger to check orchestration status

### Services
- **RiotApiService**: Handles communication with Riot Games API and rate limit parsing
- **CosmosDbService**: Manages Azure Cosmos DB operations with auto-container creation

### Models
- **LeagueEntryDTO**: Riot API response model
- **PlayerStatsDocument**: Cosmos DB document model with snapshots
- **ProcessingState**: Tracks processing progress for resumable operations
- **RateLimitInfo**: Handles rate limit information and backoff strategies

## Environment Variables

Set the following environment variables in `local.settings.json`:

```json
{
    "RIOT_API_KEY": "RGAPI-a6a22e92-3911-49eb-b9c9-f5152cf13db5",
    "AZURE_COSMOS_ENDPOINT": "https://loldb-exx.documents.azure.com:443/",
    "AZURE_COSMOS_KEY": "c6vR2JWZ7Xu4QL304R6H76pn2lrTjJ0Tcc606FEcjISSzdyRrZkKkEleNv7prCq5730ST3HocLu6ACDbqb9hLw=="
}
```

## Database Structure

### Database: `player_stats`
### Containers: One per queue type (e.g., `RANKED_SOLO_5x5`, `RANKED_FLEX_SR`, `RANKED_TFT`, `RANKED_FLEX_TT`)
### Partition Key: `/region`

### Document Structure
```json
{
    "id": "player-puuid",
    "summoner_id": "encrypted-summoner-id", 
    "puuid": "player-puuid",
    "league_id": "league-id",
    "snapshots": [
        {
            "timestamp": "2025-06-15T07:58:58.048526",
            "tier": "BRONZE",
            "rank": "I", 
            "league_points": 24,
            "wins": 67,
            "losses": 80,
            "hot_streak": false,
            "veteran": false,
            "fresh_blood": false,
            "inactive": false,
            "mini_series": null
        }
    ],
    "created_at": "2025-06-15T07:58:58.048526",
    "last_updated": "2025-06-15T07:58:58.048526",
    "region": "na1"
}
```

## Rate Limiting

The function respects Riot API rate limits:
- **Short-term**: 20 requests per 1 second
- **Long-term**: 100 requests per 2 minutes

Rate limits are tracked per region and handled as follows:
- Short rate limits (≤10 seconds): Wait inline
- Long rate limits (>10 seconds): Stop processing and resume in next cycle

## Usage

### Start Data Collection
```http
POST /api/StartPlayerStatusCollection
```

Response:
```json
{
    "message": "League Data Collection started successfully",
    "instanceId": "abc123...",
    "statusQueryGetUri": "http://localhost:7071/api/status/abc123...",
    "sendEventPostUri": "...",
    "terminatePostUri": "...",
    "rewindPostUri": "..."
}
```

### Check Status
```http
GET /api/status/{instanceId}
```

Response:
```json
{
    "instanceId": "abc123...",
    "runtimeStatus": "Running",
    "input": null,
    "output": null,
    "createdTime": "2025-06-15T...",
    "lastUpdatedTime": "2025-06-15T..."
}
```

## Running the Application

1. Install dependencies:
```bash
dotnet restore
```

2. Set up environment variables in `local.settings.json`

3. Start the function app:
```bash
func start
```

4. Start data collection:
```bash
curl -X POST http://localhost:7071/api/StartPlayerStatusCollection
```

## Testing

Run unit tests:
```bash
dotnet test
```

### Test Coverage
- **RiotApiService**: API communication and rate limit parsing
- **CosmosDbService**: Database operations and error handling
- **PlayerStatusCollectionActivity**: Data processing logic and state management
- **Model validation**: Serialization and data integrity

### Test Results
```
Test summary: total: 11, failed: 0, succeeded: 11, skipped: 0
```

## Features

### Processing Logic
- **Parallel Region Processing**: Each region is processed independently to maximize throughput
- **Sequential Queue Processing**: Within a region, queues are processed sequentially to respect rate limits
- **Pagination Support**: Handles paginated API responses automatically
- **Progress Tracking**: Maintains state for resumable processing across restarts
- **Rich Logging**: Comprehensive logging for monitoring and debugging

### Error Handling
- HTTP request failures with automatic retry logic
- Rate limit handling with intelligent backoff strategies
- Cosmos DB operation error handling with detailed logging
- Orchestration failure recovery with state preservation

### Special Cases
- **High Tiers**: Challenger, Grandmaster, and Master tiers only use division "I"
- **Empty Responses**: Automatically marks processing states as completed
- **Rate Limit Headers**: Parses and respects all Riot API rate limit information
- **Cross-Region Independence**: Rate limits are tracked independently per region

## Configuration

### Function Timeout
- Individual functions: 30 minutes (configurable)
- Orchestration: 23 hours (safety limit for long-running processes)

### Cosmos DB
- Database: `player_stats`
- Containers: Auto-created per queue type (supports both provisioned and serverless modes)
- Partition strategy: By region for optimal distribution

### Logging
- Application Insights integration for telemetry
- Structured logging with correlation IDs
- Performance counters and custom metrics

## Monitoring

The application provides detailed logging at multiple levels:

### Information Level
- Orchestration start/completion
- Processing cycle progress  
- Region processing status
- Total entries processed

### Warning Level
- Rate limit notifications
- Processing delays
- Retry attempts

### Error Level
- API failures
- Database connection errors
- Serialization issues

### Debug Level
- Individual API requests/responses
- Rate limit header parsing
- Detailed state transitions

## Processing Statistics

### Expected Scale
- **Regions**: 15 (NA1, BR1, EUN1, EUW1, JP1, KR, LA1, LA2, ME1, OC1, RU, SG2, TR1, TW2, VN2)
- **Queue Types**: 4 (RANKED_SOLO_5x5, RANKED_TFT, RANKED_FLEX_SR, RANKED_FLEX_TT)
- **Tiers**: 10 (CHALLENGER through IRON)
- **Processing States**: ~1,860 total combinations
- **Expected Runtime**: Several hours depending on rate limits and region activity

### Performance Metrics
- Processes up to 100 requests per region per 2-minute cycle
- Handles ~15 regions in parallel (1,500 requests per cycle)
- Automatic throttling based on real-time rate limit responses
- Optimized batch processing for Cosmos DB operations

## Limitations

- Maximum orchestration runtime: 23 hours (safety limit)
- Rate limits are enforced globally per region
- Historical snapshots accumulate over time (no automatic cleanup)
- Memory usage scales with concurrent region processing

## Future Enhancements

- **Incremental Updates**: Track last update timestamps to avoid full re-processing
- **Data Retention**: Implement policies for historical snapshot cleanup
- **Performance Optimization**: Region-specific optimization based on player population
- **Real-time Monitoring**: Dashboard for orchestration progress and health metrics
- **Advanced Retry**: Exponential backoff with jitter for failed requests
- **Data Validation**: Enhanced validation for API response integrity

## Troubleshooting

### Common Issues
1. **Rate Limit Errors**: Normal behavior - function will wait and retry automatically
2. **Cosmos DB Connection**: Verify connection string and network connectivity
3. **Missing API Key**: Ensure RIOT_API_KEY environment variable is properly set
4. **Storage Emulator**: Restart Azure Storage Emulator if durable functions fail to start

### Debug Tips
1. Monitor Application Insights for detailed telemetry and error traces
2. Check Cosmos DB metrics for throughput utilization and throttling
3. Use the status endpoint to track real-time orchestration progress
4. Review function app logs for specific error messages and stack traces

## Security Considerations

- **API Keys**: Stored as environment variables, never hardcoded
- **Database Access**: Uses managed identity or connection strings with minimal required permissions
- **Function Authorization**: Requires valid function keys for HTTP endpoints
- **Network Security**: Supports virtual network integration for production deployments
- **Data Privacy**: Player data is stored according to Riot Games API Terms of Service
