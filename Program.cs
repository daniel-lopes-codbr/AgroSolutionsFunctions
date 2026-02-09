using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AgroSolutions.Functions.Services;

// If the Functions host is not present (running via 'func host start'), avoid starting the worker directly.
// This prevents the gRPC channel parsing error when running the project with 'dotnet run'.
var functionsHostEnv = Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME");
var functionsScriptRoot = Environment.GetEnvironmentVariable("AzureWebJobsScriptRoot");
if (string.IsNullOrEmpty(functionsHostEnv) && string.IsNullOrEmpty(functionsScriptRoot))
{
    Console.Error.WriteLine("Azure Functions host is not detected. To run this Function locally, start the Functions host (Azure Functions Core Tools) from the project folder using:\n\n  func host start\n\nThe function worker will not start when running the project directly with 'dotnet run'. Exiting.");
    return;
}

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Register services
        services.AddScoped<IDataProcessingService, DataProcessingService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddHttpClient("api", client =>
        {
            var baseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:5000/";
            client.BaseAddress = new Uri(baseUrl);
        });
        // Register RabbitMQ listener as hosted service
        services.AddHostedService<RabbitMqListener>();
    })
    .Build();

try
{
    host.Run();
}
catch (InvalidOperationException ex) when (ex.Message.Contains("gRPC channel URI") || ex.Message.Contains("Could not be parsed"))
{
    // Host may have been disposed when the exception bubbles here, so avoid resolving services from it.
    Console.Error.WriteLine("Azure Functions host gRPC endpoint not available. To run this Function locally, start the Functions host (Azure Functions Core Tools) from the project folder using:\n\n  func host start\n\nDo not run the function project directly with 'dotnet run' without the Functions host.");
    Console.Error.WriteLine(ex.ToString());
    // Re-throw to preserve original behavior if desired
    throw;
}

