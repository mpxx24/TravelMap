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

        var blobEndpoint = settings.Value.BlobEndpoint;
        if (!string.IsNullOrEmpty(blobEndpoint))
        {
            _containerClient = new BlobContainerClient(
                new Uri($"{blobEndpoint.TrimEnd('/')}/{Constants.BlobContainerName}"),
                new DefaultAzureCredential());
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

        Directory.CreateDirectory(_dataDir);
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

    public async Task<string> GenerateShareTokenAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(email))
            throw new ArgumentException("Email cannot be null or empty.", nameof(email));

        var data = await LoadAsync(email, ct);

        // Revoke old token if present
        if (!string.IsNullOrEmpty(data.ShareToken))
            await DeleteSharePointerAsync(data.ShareToken, ct);

        var token = Guid.NewGuid().ToString("N");
        var userBlobName = GetBlobName(email);

        await WriteSharePointerAsync(token, userBlobName, ct);

        data.ShareToken = token;
        await SaveAsync(data, ct);
        return token;
    }

    public async Task RevokeShareTokenAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(email))
            throw new ArgumentException("Email cannot be null or empty.", nameof(email));

        var data = await LoadAsync(email, ct);
        if (string.IsNullOrEmpty(data.ShareToken)) return;

        await DeleteSharePointerAsync(data.ShareToken, ct);
        data.ShareToken = null;
        await SaveAsync(data, ct);
    }

    public async Task<TravelData?> LoadByShareTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(token)) return null;

        var userBlobName = await ReadSharePointerAsync(token, ct);
        if (userBlobName == null) return null;

        if (_containerClient != null)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(userBlobName);
                var response = await blobClient.DownloadContentAsync(ct);
                return JsonSerializer.Deserialize<TravelData>(response.Value.Content.ToString(), _json);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        var filePath = Path.Combine(_dataDir, userBlobName);
        if (!File.Exists(filePath)) return null;
        var text = await File.ReadAllTextAsync(filePath, ct);
        return JsonSerializer.Deserialize<TravelData>(text, _json);
    }

    public async Task<string> UploadPhotoAsync(string email, string countryCode, Stream photoStream, string contentType, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(email)) throw new ArgumentException("Email required.", nameof(email));
        if (string.IsNullOrEmpty(countryCode)) throw new ArgumentException("Country code required.", nameof(countryCode));

        var ext = ContentTypeToExtension(contentType);
        var photoId = Guid.NewGuid().ToString("N") + ext;
        var blobPath = PhotoBlobPath(GetBlobName(email)[..16], countryCode, photoId);

        if (_containerClient != null)
        {
            var blob = _containerClient.GetBlobClient(blobPath);
            await blob.UploadAsync(photoStream, overwrite: true, cancellationToken: ct);
        }
        else
        {
            var filePath = Path.Combine(_dataDir, blobPath);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            using var file = File.Create(filePath);
            await photoStream.CopyToAsync(file, ct);
        }

        var data = await LoadAsync(email, ct);
        var visit = data.Visits.FirstOrDefault(v =>
            v.CountryCode.Equals(countryCode, StringComparison.OrdinalIgnoreCase));
        if (visit == null) throw new InvalidOperationException($"No visit found for country {countryCode}.");
        visit.PhotoIds.Add(photoId);
        await SaveAsync(data, ct);
        return photoId;
    }

    public async Task DeletePhotoAsync(string email, string countryCode, string photoId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(email)) throw new ArgumentException("Email required.", nameof(email));

        var blobPath = PhotoBlobPath(GetBlobName(email)[..16], countryCode, photoId);
        if (_containerClient != null)
        {
            await _containerClient.GetBlobClient(blobPath).DeleteIfExistsAsync(cancellationToken: ct);
        }
        else
        {
            var filePath = Path.Combine(_dataDir, blobPath);
            if (File.Exists(filePath)) File.Delete(filePath);
        }

        var data = await LoadAsync(email, ct);
        var visit = data.Visits.FirstOrDefault(v =>
            v.CountryCode.Equals(countryCode, StringComparison.OrdinalIgnoreCase));
        if (visit != null)
        {
            visit.PhotoIds.Remove(photoId);
            await SaveAsync(data, ct);
        }
    }

    public async Task<(string ContentType, Stream Data)?> GetPhotoAsync(string email, string countryCode, string photoId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(email)) return null;

        var blobPath = PhotoBlobPath(GetBlobName(email)[..16], countryCode, photoId);
        var contentType = ExtensionToContentType(Path.GetExtension(photoId));

        if (_containerClient != null)
        {
            try
            {
                var blob = _containerClient.GetBlobClient(blobPath);
                var response = await blob.DownloadStreamingAsync(cancellationToken: ct);
                return (contentType, response.Value.Content);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        var filePath = Path.Combine(_dataDir, blobPath);
        if (!File.Exists(filePath)) return null;
        return (contentType, File.OpenRead(filePath));
    }

    private static string PhotoBlobPath(string emailHash16, string countryCode, string photoId) =>
        $"photos/{emailHash16}/{countryCode.ToUpperInvariant()}/{photoId}";

    private static string ContentTypeToExtension(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        _ => ".jpg"
    };

    private static string ExtensionToContentType(string ext) => ext.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };

    private async Task WriteSharePointerAsync(string token, string userBlobName, CancellationToken ct)
    {
        var pointerName = SharePointerName(token);
        if (_containerClient != null)
        {
            var blob = _containerClient.GetBlobClient(pointerName);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(userBlobName));
            await blob.UploadAsync(stream, overwrite: true, cancellationToken: ct);
        }
        else
        {
            Directory.CreateDirectory(_dataDir);
            await File.WriteAllTextAsync(Path.Combine(_dataDir, pointerName), userBlobName, ct);
        }
    }

    private async Task DeleteSharePointerAsync(string token, CancellationToken ct)
    {
        var pointerName = SharePointerName(token);
        if (_containerClient != null)
        {
            var blob = _containerClient.GetBlobClient(pointerName);
            await blob.DeleteIfExistsAsync(cancellationToken: ct);
        }
        else
        {
            var filePath = Path.Combine(_dataDir, pointerName);
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    private async Task<string?> ReadSharePointerAsync(string token, CancellationToken ct)
    {
        var pointerName = SharePointerName(token);
        if (_containerClient != null)
        {
            try
            {
                var blob = _containerClient.GetBlobClient(pointerName);
                var response = await blob.DownloadContentAsync(ct);
                return response.Value.Content.ToString().Trim();
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        var filePath = Path.Combine(_dataDir, pointerName);
        if (!File.Exists(filePath)) return null;
        return (await File.ReadAllTextAsync(filePath, ct)).Trim();
    }

    private static string SharePointerName(string token) => $"share-{token}.token";

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
