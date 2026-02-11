using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AgroSolutions.Functions.Functions;

public class AlertTimerFunctions
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<AlertTimerFunctions> _logger;

    public AlertTimerFunctions(IHttpClientFactory httpFactory, ILogger<AlertTimerFunctions> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    // Every hour at minute 0
    [Function("HourlyAlertMonitor")]
    public async Task RunHourly([TimerTrigger("0 0 * * * *")] FunctionContext context)
    {
        _logger.LogInformation("HourlyAlertMonitor triggered at {Now}", DateTime.UtcNow);
        var client = _httpFactory.CreateClient("api");
        var resp = await client.PostAsync("api/alerts", null);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Hourly alert creation failed: {Status}", resp.StatusCode);
        }
    }

    // Every day at 00:00
    [Function("DailyDeactivateAlerts")]
    public async Task RunDaily([TimerTrigger("0 0 0 * * *")] FunctionContext context)
    {
        _logger.LogInformation("DailyDeactivateAlerts triggered at {Now}", DateTime.UtcNow);
        var client = _httpFactory.CreateClient("api");
        var resp = await client.PutAsync("api/alerts/update", null);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Daily alerts deactivation failed: {Status}", resp.StatusCode);
        }
    }
}

