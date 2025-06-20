using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

public class HttpTriggers(ILogger<HttpTriggers> logger)
{
  private readonly ILogger<HttpTriggers> _logger = logger;

  [Function("")]
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

    var instanceId = await client.ScheduleNewOrchestrationInstanceAsync("PlayerMatchesCollectionOrchestrator");

    _logger.LogInformation("Started match collection orchestration with ID: {InstanceId}", instanceId);

    var response = req.CreateResponse(System.Net.HttpStatusCode.OK);

    var responseBody = new
    {
      message = "Match Collection started successfully",
      instanceId,
    };

    await response.WriteAsJsonAsync(responseBody);
    return response;
  }
}
