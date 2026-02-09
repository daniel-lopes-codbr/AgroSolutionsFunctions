namespace AgroSolutions.Functions.Services;

public interface IAnalyticsService
{
    Task<TrendAnalysis?> GetTrendAsync(Guid fieldId, string sensorType, CancellationToken cancellationToken = default);
    Task<SensorStatistics?> GetStatisticsAsync(Guid fieldId, string sensorType, CancellationToken cancellationToken = default);
}

public class TrendAnalysis
{
    public string Trend { get; set; } = string.Empty;
    public decimal? ChangeRate { get; set; }
    public string? Description { get; set; }
}

public class SensorStatistics
{
    public decimal? Average { get; set; }
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    public int ReadingCount { get; set; }
}

