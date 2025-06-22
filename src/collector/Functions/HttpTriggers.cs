using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

public class HttpTriggers(ILogger<HttpTriggers> logger)
{
  private readonly ILogger<HttpTriggers> _logger = logger;
  [Function("StartPlayerStatusCollection")]
  public async Task<HttpResponseData> StartPlayerStatusCollection(
      [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
      [DurableClient] DurableTaskClient client)
  {
    _logger.LogInformation("Starting League Data Collection orchestration");

    var instanceId = await client.ScheduleNewOrchestrationInstanceAsync("PlayerStatusCollectionOrchestrator");

    _logger.LogInformation("Started orchestration with ID: {InstanceId}", instanceId);

    var response = req.CreateResponse(System.Net.HttpStatusCode.OK);

    var responseBody = new
    {
      message = "League Data Collection started successfully",
      instanceId,
    };

    await response.WriteAsJsonAsync(responseBody);
    return response;
  }
  [Function("StartMatchCollection")]
  public async Task<HttpResponseData> StartMatchCollection(
      [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
      [DurableClient] DurableTaskClient client)
  {
    _logger.LogInformation("Starting Match Collection orchestration");

    // Parse request body for parameters
    int maxMatchesPerUnit = 1000; // Default value

    var requestBody = await req.ReadAsStringAsync();
    if (!string.IsNullOrEmpty(requestBody))
    {
      var requestData = System.Text.Json.JsonSerializer.Deserialize<MatchCollectionRequest>(requestBody);

      // Validate MaxMatchesPerUnit
      if (requestData?.MaxMatchesPerUnit > 0)
      {
        if (requestData.MaxMatchesPerUnit > 10000)
        {
          _logger.LogWarning("MaxMatchesPerUnit {RequestedValue} exceeds maximum limit, using 10000", requestData.MaxMatchesPerUnit);
          maxMatchesPerUnit = 10000;
        }
        else
        {
          maxMatchesPerUnit = requestData.MaxMatchesPerUnit;
        }
        _logger.LogInformation("Using custom MaxMatchesPerUnit: {MaxMatches}", maxMatchesPerUnit);
      }
      else if (requestData?.MaxMatchesPerUnit <= 0)
      {
        _logger.LogWarning("Invalid MaxMatchesPerUnit {RequestedValue}, using default: {Default}", requestData.MaxMatchesPerUnit, maxMatchesPerUnit);
      }
    }

    var instanceId = await client.ScheduleNewOrchestrationInstanceAsync("PlayerMatchCollectionOrchestrator", maxMatchesPerUnit);

    _logger.LogInformation("Started match collection orchestration with ID: {InstanceId}, MaxMatchesPerUnit: {MaxMatches}", instanceId, maxMatchesPerUnit);

    var response = req.CreateResponse(System.Net.HttpStatusCode.OK);

    var responseBody = new
    {
      message = "Match Collection started successfully",
      instanceId,
      maxMatchesPerUnit
    };

    await response.WriteAsJsonAsync(responseBody);
    return response;
  }

  [Function("StartMatchDataCollection")]
  public async Task<HttpResponseData> StartMatchDataCollection(
      [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
      [DurableClient] DurableTaskClient client)
  {
    _logger.LogInformation("Starting Match Data Collection orchestration");

    var instanceId = await client.ScheduleNewOrchestrationInstanceAsync("MatchDataCollectionOrchestrator");

    _logger.LogInformation("Started match data collection orchestration with ID: {InstanceId}", instanceId);

    var response = req.CreateResponse(System.Net.HttpStatusCode.OK);

    var responseBody = new
    {
      message = "Match Data Collection started successfully",
      instanceId,
    };

    await response.WriteAsJsonAsync(responseBody);
    return response;
  }
}
