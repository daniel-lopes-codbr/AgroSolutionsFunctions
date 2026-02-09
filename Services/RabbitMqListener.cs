using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AgroSolutions.Functions.Services;

public class RabbitMqListener : IHostedService, IDisposable
{
    private readonly ILogger<RabbitMqListener> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private IConnection? _connection;
    private IModel? _channel;
    private string _queueName;
    private readonly string _apiBaseUrl;
    private readonly string _hostName;
    private readonly string _user;
    private readonly string _password;

    public RabbitMqListener(ILogger<RabbitMqListener> logger, IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _queueName = config["RABBITMQ_QUEUE"] ?? "sensor.readings";
        _apiBaseUrl = config["API_BASE_URL"] ?? "http://localhost:5000/";
        _hostName = config["RABBITMQ_HOST"] ?? "localhost";
        _user = config["RABBITMQ_USER"] ?? "guest";
        _password = config["RABBITMQ_PASSWORD"] ?? "guest";
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory() { HostName = _hostName, UserName = _user, Password = _password };
        try
        {
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(queue: _queueName, durable: true, exclusive: false, autoDelete: false);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                try
                {
                    await ProcessMessageAsync(message);
                    _channel?.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing RabbitMQ message");
                    // Optionally nack/requeue
                    _channel?.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);
            _logger.LogInformation("RabbitMQ listener started on queue {Queue}", _queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start RabbitMQ listener");
        }

        return Task.CompletedTask;
    }

    public async Task ProcessMessageAsync(string message)
    {
        // Expect message to be SensorReadingDto JSON
        var client = _httpClientFactory.CreateClient("api");
        var content = new StringContent(message, Encoding.UTF8, "application/json");
        var resp = await client.PostAsync("api/ingestion/single", content);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            _logger.LogWarning("API returned {Status} when posting sensor reading: {Body}", resp.StatusCode, body);
        }
        else
        {
            _logger.LogInformation("Sensor reading posted to API successfully");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping RabbitMQ listener");
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try
        {
            _channel?.Close();
            _connection?.Close();
        }
        catch { }
    }
}

