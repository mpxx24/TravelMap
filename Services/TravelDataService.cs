using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using TravelMap.Models;
using TravelMap.Settings;

namespace TravelMap.Services;

public class TravelDataService : ITravelDataService
{
    private readonly string _dataDir;
    private readonly BlobContainerClient? _containerClient;
    private readonly ILogger<TravelDataService> _logger;
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public TravelDataService(IWebHostEnvironment env, IOptions<TravelDataSettings> settings, ILogger<TravelDataService> logger)
    {
        _logger = logger;
        _dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(_dataDir);

        var blobEndpoint = settings.Value.BlobEndpoint;
        if (!string.IsNullOrEmpty(blobEndpoint))
        {
            _containerClient = new BlobContainerClient(
                new Uri($"{blobEndpoint.TrimEnd('/')}/{Constants.BlobContainerName}"),
                new DefaultAzureCredential());
            _containerClient.CreateIfNotExists();
        }
    }

    public async Task<TravelData> LoadAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(email))
            throw new ArgumentException("Email cannot be null or empty.", nameof(email));

        var blobName = GetBlobName(email);

        if (_containerClient != null)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(blobName);
                var response = await blobClient.DownloadContentAsync(ct);
                return JsonSerializer.Deserialize<TravelData>(response.Value.Content.ToString(), _json)
                    ?? NewData(email);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return NewData(email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load travel data from blob for {Email}", email);
                return NewData(email);
            }
        }

        var filePath = Path.Combine(_dataDir, blobName);
        if (!File.Exists(filePath)) return NewData(email);

        try
        {
            var text = await File.ReadAllTextAsync(filePath, ct);
            return JsonSerializer.Deserialize<TravelData>(text, _json) ?? NewData(email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load travel data from file for {Email}", email);
            return NewData(email);
        }
    }

    public async Task SaveAsync(TravelData data, CancellationToken ct = default)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        data.LastModified = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(data, _json);
        var blobName = GetBlobName(data.UserEmail);

        if (_containerClient != null)
        {
            var blobClient = _containerClient.GetBlobClient(blobName);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: ct);
            return;
        }

        await File.WriteAllTextAsync(Path.Combine(_dataDir, blobName), json, ct);
    }

    public async Task<CountryVisit> UpsertVisitAsync(string email, CountryVisit visit, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(email))
            throw new ArgumentException("Email cannot be null or empty.", nameof(email));
        if (visit == null) throw new ArgumentNullException(nameof(visit));

        var data = await LoadAsync(email, ct);
        var index = data.Visits.FindIndex(v =>
            v.CountryCode.Equals(visit.CountryCode, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
            data.Visits[index] = visit;
        else
            data.Visits.Add(visit);

        await SaveAsync(data, ct);
        return visit;
    }

    public async Task DeleteVisitAsync(string email, string countryCode, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(email))
            throw new ArgumentException("Email cannot be null or empty.", nameof(email));
        if (string.IsNullOrEmpty(countryCode))
            throw new ArgumentException("Country code cannot be null or empty.", nameof(countryCode));

        var data = await LoadAsync(email, ct);
        data.Visits.RemoveAll(v =>
            v.CountryCode.Equals(countryCode, StringComparison.OrdinalIgnoreCase));
        await SaveAsync(data, ct);
    }

    private static string GetBlobName(string email)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant()));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant() + ".json";
    }

    private static TravelData NewData(string email) => new()
    {
        UserEmail = email,
        Visits = new List<CountryVisit>(),
        LastModified = DateTime.UtcNow
    };
}
