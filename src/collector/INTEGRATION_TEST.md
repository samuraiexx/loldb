# Integration Test Script for League Data Collector

## Prerequisites
- Azure Functions Core Tools installed
- Azure Storage Emulator running (for local development)
- Valid Riot API key in environment variables
- Valid Azure Cosmos DB connection string

## Running the Function App

1. Start the Azure Storage Emulator:
```bash
# Windows
azurite --silent --location c:\azurite --debug c:\azurite\debug.log
```

2. Start the Function App:
```bash
func start
```

3. Test the HTTP trigger:
```bash
# Test basic endpoint
curl http://localhost:7071/api/MyHttpTrigger

# Start data collection
curl -X POST http://localhost:7071/api/StartLeagueDataCollection

# Check orchestration status (replace {instanceId} with actual ID from previous response)
curl http://localhost:7071/api/status/{instanceId}
```

## Expected Workflow

1. **Orchestration Start**: The durable function orchestrator begins and initializes processing states for all regions, queue types, tiers, and divisions.

2. **Parallel Region Processing**: Each region is processed in parallel, with up to 100 requests per 2-minute window.

3. **Rate Limit Handling**: 
   - Short rate limits (≤10 seconds): Wait inline
   - Long rate limits (>10 seconds): Stop processing and resume in next cycle

4. **Data Processing**: Each League entry is transformed into a PlayerStatsDocument and stored in Cosmos DB.

5. **Progress Tracking**: The orchestrator logs progress and tracks completion status.

## Monitoring

### Logs to Watch For:
- `Starting League Data Collection Orchestrator`
- `Initialized {Count} processing states`
- `Processing {RegionCount} regions in parallel`
- `Progress: {Completed}/{Total} states completed`
- `Rate limit hit for {Region}`
- `Orchestration completed`

### Key Metrics:
- Number of processing states: ~2,400 (15 regions × 4 queues × ~40 tier/division combinations)
- Expected completion time: Several hours depending on rate limits
- Total player entries: Varies by region and tier popularity

## Sample Response Data

### Orchestration Status Response:
```json
{
    "instanceId": "abc123...",
    "runtimeStatus": "Running",
    "createdTime": "2025-06-15T...",
    "lastUpdatedTime": "2025-06-15T..."
}
```

### Cosmos DB Document:
```json
{
    "id": "player-puuid",
    "summoner_id": "encrypted-summoner-id",
    "puuid": "player-puuid",
    "league_id": "league-id",
    "snapshots": [...],
    "created_at": "2025-06-15T...",
    "last_updated": "2025-06-15T...",
    "region": "na1"
}
```

## Testing with Limited Scope

For testing purposes, you can modify the `Constants` class to limit the scope:

```csharp
// Test with fewer regions
public static readonly string[] Regions = { "NA1", "BR1" };

// Test with fewer queue types
public static readonly string[] QueueTypes = { "RANKED_SOLO_5x5" };

// Test with fewer tiers
public static readonly string[] Tiers = { "GOLD", "SILVER" };
```

## Troubleshooting

### Common Issues:
1. **Rate Limit Errors**: Normal behavior, the function will wait and retry
2. **Cosmos DB Connection Errors**: Check connection string and network connectivity
3. **Missing API Key**: Ensure RIOT_API_KEY environment variable is set
4. **Storage Emulator Issues**: Restart the Azure Storage Emulator

### Debug Tips:
1. Check Application Insights for detailed telemetry
2. Monitor Cosmos DB metrics for throughput and errors
3. Use the status endpoint to track orchestration progress
4. Review function logs for specific error messages

## Performance Considerations

- Each region processes independently, respecting individual rate limits
- The function uses batched upserts to optimize Cosmos DB operations
- Rate limit windows are tracked per region to maximize throughput
- Long-running orchestrations automatically include checkpointing

## Security Notes

- API keys are stored as environment variables
- Cosmos DB uses connection strings with appropriate permissions
- Function authorization level is set to "Function" requiring valid function keys
