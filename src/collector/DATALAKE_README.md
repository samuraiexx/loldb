# Azure Data Lake Service Implementation

## Overview

This application uses Azure Data Lake Storage Gen2 exclusively for storing League of Legends match and player statistics data. The service provides a robust, scalable, and cost-effective solution for managing large datasets with comprehensive analytics capabilities.

## Architecture

### File Organization

The Azure Data Lake service organizes data in a hierarchical structure:

```
loldb-data/                          # File System
├── player-stats/                    # Player statistics
│   ├── ranked-solo-5x5/            # Queue type (normalized)
│   │   ├── na1/                     # Region (normalized)
│   │   │   ├── [player1-puuid]/     # Individual player directory
│   │   │   │   ├── snapshot-20240627-143022.json  # Timestamped snapshots
│   │   │   │   ├── snapshot-20240627-150030.json
│   │   │   │   └── ...
│   │   │   ├── [player2-puuid]/
│   │   │   │   ├── snapshot-20240627-143045.json
│   │   │   │   └── ...
│   │   │   └── ...
│   │   ├── euw1/
│   │   └── ...
│   └── ...
└── matches/                         # Match data
    ├── na1/                         # Region (actual region, not match region)
    │   ├── [match1-id]/             # Individual match directories
    │   │   ├── match.json          # Match data
    │   │   └── timeline.json       # Match timeline data
    │   ├── [match2-id]/
    │   │   ├── match.json
    │   │   └── timeline.json
    │   └── ...
    ├── euw1/
    ├── br1/
    ├── kr/
    └── ...
```

### Key Features

1. **Hierarchical Storage**: Data is organized by type, queue, and region for efficient querying
2. **Individual Player Directories**: Each player has their own directory with timestamped snapshots
3. **Historical Data**: Maintains multiple snapshots over time for trend analysis
4. **Per-Match Directories**: Each match has its own directory containing separate match and timeline data files
5. **Timeline Data Support**: Stores detailed timeline data separately from match data for efficient access
6. **Normalized Paths**: Queue types and regions are normalized (e.g., `RANKED_SOLO_5x5` → `ranked-solo-5x5`, `NA1` → `na1`)
7. **JSON File Format**: Each document is stored as a separate JSON file
8. **Concurrent Operations**: Uses semaphores to limit concurrent file operations
9. **Error Handling**: Comprehensive error handling and logging
10. **High Performance**: Optimized for large-scale data operations

## Configuration

### Environment Variables

- `AZURE_STORAGE_CONNECTION_STRING`: Azure Storage account connection string
- `DATA_SERVICE_TYPE`: Should be set to `"datalake"` (this is the only supported service)

### Example Configuration

```bash
# Azure Data Lake Storage configuration
DATA_SERVICE_TYPE=datalake
AZURE_STORAGE_CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName=yourstorageaccount;AccountKey=yourkey;EndpointSuffix=core.windows.net
```

## Performance Considerations

### Advantages of Azure Data Lake Storage

1. **Cost**: Significantly lower storage costs for large datasets
2. **Scalability**: Nearly unlimited storage capacity
3. **Analytics**: Excellent integration with Azure analytics services (Synapse, Databricks)
4. **Backup**: Built-in geo-redundancy and backup options
5. **Performance**: Optimized for high-throughput operations
6. **Flexibility**: Support for various file formats and data structures

### Considerations

1. **Query Performance**: File-based storage may require different query patterns than traditional databases
2. **Consistency**: Eventual consistency model for file operations
3. **Indexing**: No automatic indexing - queries require file enumeration or metadata files
4. **Transactions**: No ACID transactions across multiple files - operations are eventually consistent

## Usage Examples

### Service Usage

The application automatically uses Azure Data Lake Storage for all data operations:

```csharp
// The application uses Data Lake Storage exclusively
public class MyFunction
{
    private readonly IDataService _dataService;

    public MyFunction(IDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task ProcessData()
    {
        // All operations use Azure Data Lake Storage
        var playerStats = await _dataService.GetPlayerStatsAsync(puuid, queueType, region);
        // ... process data
        await _dataService.UpsertPlayerStatsAsync(updatedStats, queueType);
        
        // Work with match and timeline data
        var match = await _dataService.GetMatchAsync(matchId, region);
        var timeline = await _dataService.GetMatchTimelineAsync(matchId, region);
        
        // Store updated match and timeline data
        await _dataService.UpsertMatchAsync(updatedMatch);
        await _dataService.UpsertMatchTimelineAsync(matchId, region, timelineData);
    }
}
```

### Direct Service Usage

If you need to use the Data Lake service directly:

```csharp
public class MyFunction
{
    private readonly IDataLakeService _dataLakeService;

    public MyFunction(IDataLakeService dataLakeService)
    {
        _dataLakeService = dataLakeService;
    }
}
```

## Getting Started

### Setting Up Azure Data Lake Storage

1. Create an Azure Storage account with Data Lake Gen2 enabled
2. Configure your environment variables
3. Initialize the application - the service will automatically create the file system
4. Start processing League of Legends data

### Data Organization

The service automatically organizes your data in an optimal hierarchical structure that supports:
- Efficient querying by player, region, and queue type
- Historical analysis with timestamped snapshots
- Separate match and timeline data storage
- Analytics integration with Azure services

## Monitoring and Logging

The service includes comprehensive logging for:
- File operations (upload, download, delete)
- Batch operations progress
- Error conditions and retries
- Performance metrics

Use Azure Monitor and Application Insights to track:
- Storage transaction metrics
- Function execution times
- Error rates and patterns

## Best Practices

1. **Batch Operations**: Use batch methods for bulk data operations
2. **Parallel Processing**: The service automatically limits concurrency to prevent throttling
3. **Error Handling**: Always handle potential file not found scenarios
4. **Monitoring**: Set up alerts for storage account metrics and errors
5. **Data Lifecycle**: Implement data archival policies for old match data

## Future Enhancements

1. **Caching Layer**: Add Redis cache for frequently accessed data
2. **Indexing**: Implement metadata files for faster queries
3. **Compression**: Add gzip compression for larger files
4. **Partitioning**: Implement time-based partitioning for better performance
5. **Analytics Integration**: Direct integration with Azure Synapse for analytics

### File Naming Conventions

#### Player Stats Files
- **Path**: `player-stats/{normalized-queue-type}/{normalized-region}/{puuid}/snapshot-{timestamp}.json`
- **Queue Type Normalization**: `RANKED_SOLO_5x5` → `ranked-solo-5x5`
- **Region Normalization**: `NA1` → `na1`, `EUW1` → `euw1`
- **Timestamp Format**: `yyyyMMdd-HHmmss` (e.g., `20240627-143022`)
- **Example**: `player-stats/ranked-solo-5x5/na1/abc123-def456-ghi789/snapshot-20240627-143022.json`

#### Match Files
- **Path**: `matches/{region}/{match-id}/match.json` and `matches/{region}/{match-id}/timeline.json`
- **Region**: Uses actual region (e.g., `NA1`, `EUW1`) not match region (`americas`, `europe`)
- **Match Data Example**: `matches/NA1/NA1_4567890123/match.json`
- **Timeline Data Example**: `matches/NA1/NA1_4567890123/timeline.json`

#### Timeline Data Support
Azure Data Lake Storage provides comprehensive support for storing and retrieving match timeline data:
- **Separate Storage**: Timeline data is stored in separate files from match data for optimal performance
- **Automatic Directory Creation**: Per-match directories are created automatically when storing data
- **Independent Operations**: Match data and timeline data can be stored and retrieved independently
- **Optional Timeline**: Timeline data is optional - matches can exist without timeline data
- **High Performance**: Optimized file organization for fast timeline data access

#### Latest Snapshot Retrieval
When retrieving player stats, the service automatically finds and returns the most recent snapshot based on the timestamp in the filename. This allows for:
- Historical data analysis
- Tracking player progression over time
- Point-in-time queries for specific dates
