using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AgroSolutionsFunctions;

public class IngestionEntryPoint
{
    private readonly ILogger<IngestionEntryPoint> _logger;
    private readonly HttpClient _httpClient;

    public IngestionEntryPoint(
        ILogger<IngestionEntryPoint> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;

        _httpClient = httpClientFactory.CreateClient();
    }

    [Function("Ingestion-Queue")]
    public async Task Run(
        [ServiceBusTrigger("ingestion-data-queue", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("Message ID: {id}", message.MessageId);
        _logger.LogInformation("Message Body: {body}", message.Body);
        _logger.LogInformation("Message Content-Type: {contentType}", message.ContentType);

        var messageBody = message.Body.ToString();

        var response = await _httpClient.PostAsync(
            "http://localhost:5000/api/ingestion/single",
            new StringContent(messageBody, System.Text.Encoding.UTF8, "application/json"));

        _logger.LogInformation("Response Status Code: {statusCode}", response.StatusCode);

        // Complete the message
        await messageActions.CompleteMessageAsync(message);
    }

    [Function("Ingestion-Timer-Run")]
    public async Task TimerRun(
        [TimerTrigger("*/1 * * * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation("Timer trigger executed at: {time}", DateTime.Now);

        var content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("http://localhost:5000/api/alerts", content);

        _logger.LogInformation("Response Status Code: {statusCode}", response.StatusCode);
    }

    [Function("Ingestion-Timer-Disable")]
    public async Task TimerDisable(
        [TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation("Timer trigger executed at: {time}", DateTime.Now);

        var content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PutAsync("http://localhost:5000/api/alerts/update", content);

        _logger.LogInformation("Response Status Code: {statusCode}", response.StatusCode);
    }

}