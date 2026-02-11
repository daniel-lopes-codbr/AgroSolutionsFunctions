using AgroSolutions.Functions.Services;
using AgroSolutions.Domain.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace AgroSolutions.Functions.Functions;

/// <summary>
/// Azure Function to process sensor data with intelligence
/// </summary>
public class ProcessSensorDataFunction
{
    private readonly IDataProcessingService _processingService;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<ProcessSensorDataFunction> _logger;

    public ProcessSensorDataFunction(
        IDataProcessingService processingService,
        IAnalyticsService analyticsService,
        ILogger<ProcessSensorDataFunction> logger)
    {
        _processingService = processingService;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    [Function("ProcessSensorReading")]
    public async Task<HttpResponseData> ProcessSensorReading(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "process/reading")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Processing sensor reading request");

        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var reading = JsonSerializer.Deserialize<SensorReading>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (reading == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid sensor reading data");
                return errorResponse;
            }

            if (_analyticsService is AnalyticsService analytics)
            {
                analytics.AddReading(reading);
            }

            var processed = await _processingService.ProcessReadingAsync(reading);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                success = true,
                processedReading = new
                {
                    id = processed.OriginalReading.Id,
                    isAnomaly = processed.IsAnomaly,
                    anomalyReason = processed.AnomalyReason,
                    normalizedValue = processed.NormalizedValue,
                    insights = processed.Insights,
                    processedAt = processed.ProcessedAt
                }
            }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing sensor reading");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("ProcessSensorBatch")]
    public async Task<HttpResponseData> ProcessSensorBatch(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "process/batch")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Processing sensor batch request");

        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var readings = JsonSerializer.Deserialize<List<SensorReading>>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (readings == null || readings.Count == 0)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid or empty batch data");
                return errorResponse;
            }

            if (_analyticsService is AnalyticsService analytics)
            {
                foreach (var reading in readings)
                {
                    analytics.AddReading(reading);
                }
            }

            var processed = await _processingService.ProcessBatchAsync(readings);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                success = true,
                processedCount = processed.Count,
                anomalyCount = processed.Count(p => p.IsAnomaly),
                processedReadings = processed.Select(p => new
                {
                    id = p.OriginalReading.Id,
                    isAnomaly = p.IsAnomaly,
                    anomalyReason = p.AnomalyReason,
                    insights = p.Insights
                })
            }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing sensor batch");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }
}

