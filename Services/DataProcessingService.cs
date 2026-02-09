using AgroSolutions.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgroSolutions.Functions.Services;

public class DataProcessingService : IDataProcessingService
{
    private readonly ILogger<DataProcessingService> _logger;
    private readonly IAnalyticsService _analyticsService;

    private readonly Dictionary<string, (decimal Min, decimal Max)> _sensorThresholds = new()
    {
        { "Temperature", (0m, 50m) },
        { "Humidity", (0m, 100m) },
        { "SoilMoisture", (0m, 100m) },
        { "pH", (4m, 9m) }
    };

    public DataProcessingService(ILogger<DataProcessingService> logger, IAnalyticsService analyticsService)
    {
        _logger = logger;
        _analyticsService = analyticsService;
    }

    public async Task<ProcessedReading> ProcessReadingAsync(SensorReading reading, CancellationToken cancellationToken = default)
    {
        string sensorType = reading.SensorType ?? (reading.SoilMoisture.HasValue ? "SoilMoisture" : (reading.AirTemperature.HasValue ? "AirTemperature" : "Telemetry"));
        decimal value = reading.Value ?? (reading.SoilMoisture ?? reading.AirTemperature ?? reading.Precipitation ?? 0m);
        string unit = reading.Unit ?? (sensorType == "SoilMoisture" ? "Percent" : sensorType == "AirTemperature" ? "Celsius" : string.Empty);

        _logger.LogInformation("Processing reading: {SensorType} = {Value} {Unit} for Field {FieldId}", sensorType, value, unit, reading.FieldId);

        var processed = new ProcessedReading { OriginalReading = reading, ProcessedAt = DateTime.UtcNow };
        processed.NormalizedValue = NormalizeValue(sensorType, value, unit);
        var anomalyResult = DetectAnomaly(reading, sensorType, value, unit);
        processed.IsAnomaly = anomalyResult.IsAnomaly;
        processed.AnomalyReason = anomalyResult.Reason;
        processed.Insights = await GenerateInsightsAsync(reading, cancellationToken);

        _logger.LogInformation("Processed reading: Anomaly={IsAnomaly}, Insights={InsightCount}", processed.IsAnomaly, processed.Insights?.Count ?? 0);
        return processed;
    }

    public async Task<List<ProcessedReading>> ProcessBatchAsync(IEnumerable<SensorReading> readings, CancellationToken cancellationToken = default)
    {
        var readingsList = readings.ToList();
        _logger.LogInformation("Processing batch of {Count} readings", readingsList.Count);
        var tasks = readingsList.Select(reading => ProcessReadingAsync(reading, cancellationToken));
        var results = await Task.WhenAll(tasks);
        _logger.LogInformation("Batch processing completed: {TotalCount} processed, {AnomalyCount} anomalies detected", results.Length, results.Count(r => r.IsAnomaly));
        return results.ToList();
    }

    private decimal? NormalizeValue(string sensorType, decimal value, string unit)
    {
        return sensorType.ToLowerInvariant() switch
        {
            "temperature" when unit.Equals("Fahrenheit", StringComparison.OrdinalIgnoreCase) => (value - 32) * 5 / 9,
            "temperature" when unit.Equals("Celsius", StringComparison.OrdinalIgnoreCase) => value,
            "humidity" or "soilmoisture" when unit.Equals("Percent", StringComparison.OrdinalIgnoreCase) => value,
            _ => value
        };
    }

    private (bool IsAnomaly, string? Reason) DetectAnomaly(SensorReading reading, string sensorType, decimal value, string unit)
    {
        if (reading.IsRichInPests == true) return (true, "Pest indicators present");
        if (!_sensorThresholds.TryGetValue(sensorType, out var threshold)) return (false, null);
        var normalizedValue = NormalizeValue(sensorType, value, unit) ?? value;
        if (normalizedValue < threshold.Min) return (true, $"Value {normalizedValue} is below minimum threshold {threshold.Min}");
        if (normalizedValue > threshold.Max) return (true, $"Value {normalizedValue} is above maximum threshold {threshold.Max}");
        return (false, null);
    }

    private async Task<Dictionary<string, object>> GenerateInsightsAsync(SensorReading reading, CancellationToken cancellationToken)
    {
        var insights = new Dictionary<string, object>();
        var sensorType = reading.SensorType ?? (reading.SoilMoisture.HasValue ? "SoilMoisture" : (reading.AirTemperature.HasValue ? "AirTemperature" : "Telemetry"));
        var trend = await _analyticsService.GetTrendAsync(reading.FieldId, sensorType, cancellationToken);
        if (trend != null) insights["trend"] = trend;
        var stats = await _analyticsService.GetStatisticsAsync(reading.FieldId, sensorType, cancellationToken);
        if (stats != null) insights["statistics"] = stats;
        var recommendations = GetRecommendations(reading);
        if (recommendations.Any()) insights["recommendations"] = recommendations;
        return insights;
    }

    private List<string> GetRecommendations(SensorReading reading)
    {
        var recommendations = new List<string>();
        var sensorTypeForRecommendations = reading.SensorType ?? (reading.SoilMoisture.HasValue ? "SoilMoisture" : (reading.AirTemperature.HasValue ? "AirTemperature" : string.Empty));
        var valueForRecommendations = reading.Value ?? (reading.SoilMoisture ?? reading.AirTemperature ?? reading.Precipitation ?? 0m);
        var unitForRecommendations = reading.Unit ?? (sensorTypeForRecommendations == "SoilMoisture" ? "Percent" : sensorTypeForRecommendations == "AirTemperature" ? "Celsius" : string.Empty);
        var normalizedValue = NormalizeValue(sensorTypeForRecommendations, valueForRecommendations, unitForRecommendations) ?? valueForRecommendations;

        switch (sensorTypeForRecommendations.ToLowerInvariant())
        {
            case "temperature":
                if (normalizedValue > 35) recommendations.Add("High temperature detected. Consider irrigation or shading.");
                else if (normalizedValue < 10) recommendations.Add("Low temperature detected. Consider protective measures.");
                break;
            case "humidity":
                if (normalizedValue < 30) recommendations.Add("Low humidity detected. Consider increasing irrigation.");
                else if (normalizedValue > 80) recommendations.Add("High humidity detected. Monitor for fungal diseases.");
                break;
            case "soilmoisture":
                if (normalizedValue < 30) recommendations.Add("Low soil moisture. Irrigation recommended.");
                else if (normalizedValue > 80) recommendations.Add("High soil moisture. Risk of root rot.");
                break;
        }

        return recommendations;
    }
}

