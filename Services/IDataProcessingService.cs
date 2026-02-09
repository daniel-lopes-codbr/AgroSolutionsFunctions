using AgroSolutions.Domain.Entities;

namespace AgroSolutions.Functions.Services;

public interface IDataProcessingService
{
    Task<ProcessedReading> ProcessReadingAsync(SensorReading reading, CancellationToken cancellationToken = default);
    Task<List<ProcessedReading>> ProcessBatchAsync(IEnumerable<SensorReading> readings, CancellationToken cancellationToken = default);
}

public class ProcessedReading
{
    public SensorReading OriginalReading { get; set; } = null!;
    public bool IsAnomaly { get; set; }
    public string? AnomalyReason { get; set; }
    public decimal? NormalizedValue { get; set; }
    public Dictionary<string, object>? Insights { get; set; }
    public DateTime ProcessedAt { get; set; }
}

