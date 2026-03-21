using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using TravelMap.Settings;

namespace TravelMap.Services;

public class BlobContainerInitializer : IHostedService
{
    private readonly TravelDataSettings _settings;
    private readonly ILogger<BlobContainerInitializer> _logger;

    public BlobContainerInitializer(IOptions<TravelDataSettings> settings, ILogger<BlobContainerInitializer> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_settings.BlobEndpoint))
        {
            _logger.LogInformation("No BlobEndpoint configured — skipping container initialisation (local dev mode).");
            return;
        }

        var containerUri = new Uri($"{_settings.BlobEndpoint.TrimEnd('/')}/{Constants.BlobContainerName}");
        var client = new BlobContainerClient(containerUri, new DefaultAzureCredential());

        await client.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        _logger.LogInformation("Blob container '{Container}' is ready.", Constants.BlobContainerName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
