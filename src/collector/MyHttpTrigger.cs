using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.DurableTask.Client;
using Microsoft.Azure.Functions.Worker.Http;

namespace collector;

public class MyHttpTrigger
{
  private readonly ILogger<MyHttpTrigger> _logger;

  public MyHttpTrigger(ILogger<MyHttpTrigger> logger)
  {
    _logger = logger;
  }

  [Function("StartLeagueDataCollection")]
  public async Task<HttpResponseData> StartLeagueDataCollection(
      [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
      [DurableClient] DurableTaskClient client)
  {
    _logger.LogInformation("Starting League Data Collection orchestration");

    try
    {
      var instanceId = await client.ScheduleNewOrchestrationInstanceAsync("LeagueDataOrchestrator");

      _logger.LogInformation("Started orchestration with ID: {InstanceId}", instanceId);

      var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
      response.Headers.Add("Content-Type", "application/json");

      var responseBody = new
      {
        message = "League Data Collection started successfully",
        instanceId = instanceId,
        statusQueryGetUri = $"{req.Url.Scheme}://{req.Url.Host}/api/status/{instanceId}",
        sendEventPostUri = $"{req.Url.Scheme}://{req.Url.Host}/runtime/webhooks/durabletask/instances/{instanceId}/raiseEvent/{{eventName}}",
        terminatePostUri = $"{req.Url.Scheme}://{req.Url.Host}/runtime/webhooks/durabletask/instances/{instanceId}/terminate",
        rewindPostUri = $"{req.Url.Scheme}://{req.Url.Host}/runtime/webhooks/durabletask/instances/{instanceId}/rewind"
      };

      await response.WriteAsJsonAsync(responseBody);
      return response;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error starting League Data Collection orchestration");
      var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
      await errorResponse.WriteStringAsync("Internal server error");
      return errorResponse;
    }
  }

  [Function("GetOrchestrationStatus")]
  public async Task<HttpResponseData> GetOrchestrationStatus(
      [HttpTrigger(AuthorizationLevel.Function, "get", Route = "status/{instanceId}")] HttpRequestData req,
      [DurableClient] DurableTaskClient client,
      string instanceId)
  {
    _logger.LogInformation("Getting orchestration status for instance: {InstanceId}", instanceId);

    try
    {
      var status = await client.GetInstanceAsync(instanceId);

      if (status == null)
      {
        var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
        await notFoundResponse.WriteAsJsonAsync(new { message = "Orchestration instance not found" });
        return notFoundResponse;
      }

      var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
      await response.WriteAsJsonAsync(new
      {
        instanceId = status.InstanceId,
        runtimeStatus = status.RuntimeStatus.ToString(),
        input = status.SerializedInput,
        output = status.SerializedOutput,
        createdTime = status.CreatedAt,
        lastUpdatedTime = status.LastUpdatedAt
      });

      return response;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting orchestration status for instance: {InstanceId}", instanceId);
      var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
      await errorResponse.WriteStringAsync("Internal server error");
      return errorResponse;
    }
  }

  [Function("MyHttpTrigger")]
  public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
  {
    _logger.LogInformation("C# HTTP trigger function processed a request.");

    var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
    response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
    await response.WriteStringAsync("Welcome to Azure Functions! Use POST /api/StartLeagueDataCollection to begin data collection.");

    return response;
  }
}
