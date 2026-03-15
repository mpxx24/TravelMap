using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using Azure.Storage.Blobs;
using TravelMap.Models;

namespace TravelMap.Services;

public class TravelDataService
{
    private readonly string _dataDir;
    private readonly BlobContainerClient? _containerClient;
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public TravelDataService(IWebHostEnvironment env, IConfiguration config)
    {
        _dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(_dataDir);

        var blobEndpoint = config["Storage:BlobEndpoint"];
        if (!string.IsNullOrEmpty(blobEndpoint))
        {
            _containerClient = new BlobContainerClient(
                new Uri($"{blobEndpoint.TrimEnd('/')}/travelmap"),
                new DefaultAzureCredential());
            _containerClient.CreateIfNotExists();
        }
    }

    public TravelData Load(string email)
    {
        var blobName = GetBlobName(email);

        if (_containerClient != null)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(blobName);
                var response = blobClient.DownloadContent();
                return JsonSerializer.Deserialize<TravelData>(response.Value.Content.ToString(), _json)
                    ?? NewData(email);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return NewData(email);
            }
            catch { return NewData(email); }
        }

        var filePath = Path.Combine(_dataDir, blobName);
        if (!File.Exists(filePath)) return NewData(email);
        try
        {
            var text = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<TravelData>(text, _json) ?? NewData(email);
        }
        catch { return NewData(email); }
    }

    public void Save(TravelData data)
    {
        data.LastModified = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(data, _json);
        var blobName = GetBlobName(data.UserEmail);

        if (_containerClient != null)
        {
            var blobClient = _containerClient.GetBlobClient(blobName);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            blobClient.Upload(stream, overwrite: true);
            return;
        }

        File.WriteAllText(Path.Combine(_dataDir, blobName), json);
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
