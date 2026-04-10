using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using TravelMap.Models;
using TravelMap.Services;
using TravelMap.Settings;

namespace TravelMap.Tests;

[TestFixture]
public class TravelDataServiceTests
{
    private string _tempDir = null!;
    private TravelDataService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);

        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(_tempDir);

        var settings = Options.Create(new TravelDataSettings { BlobEndpoint = null });
        var logger = Mock.Of<ILogger<TravelDataService>>();

        _service = new TravelDataService(env.Object, settings, logger);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void LoadAsync_ThrowsArgumentException_WhenEmailIsNull()
    {
        Assert.ThrowsAsync<ArgumentException>(() => _service.LoadAsync(null!));
    }

    [Test]
    public void LoadAsync_ThrowsArgumentException_WhenEmailIsEmpty()
    {
        Assert.ThrowsAsync<ArgumentException>(() => _service.LoadAsync(""));
    }

    [Test]
    public async Task LoadAsync_ReturnsNewData_WhenFileDoesNotExist()
    {
        var result = await _service.LoadAsync("new@example.com");

        Assert.That(result.UserEmail, Is.EqualTo("new@example.com"));
        Assert.That(result.Visits, Is.Empty);
    }

    [Test]
    public async Task UpsertVisitAsync_AddsNewVisit_WhenCountryNotExists()
    {
        var visit = new CountryVisit { CountryCode = "POL", CountryName = "Poland", VisitType = VisitType.Mainland };

        await _service.UpsertVisitAsync("user@example.com", visit);

        var data = await _service.LoadAsync("user@example.com");
        Assert.That(data.Visits, Has.Count.EqualTo(1));
        Assert.That(data.Visits[0].CountryCode, Is.EqualTo("POL"));
    }

    [Test]
    public async Task UpsertVisitAsync_UpdatesExistingVisit_WhenCountryExists()
    {
        var initial = new CountryVisit { CountryCode = "POL", CountryName = "Poland", VisitType = VisitType.Mainland };
        await _service.UpsertVisitAsync("user@example.com", initial);

        var updated = new CountryVisit { CountryCode = "POL", CountryName = "Poland", VisitType = VisitType.Both };
        await _service.UpsertVisitAsync("user@example.com", updated);

        var data = await _service.LoadAsync("user@example.com");
        Assert.That(data.Visits, Has.Count.EqualTo(1));
        Assert.That(data.Visits[0].VisitType, Is.EqualTo(VisitType.Both));
    }

    [Test]
    public async Task DeleteVisitAsync_RemovesVisit_WhenCountryExists()
    {
        var visit = new CountryVisit { CountryCode = "POL", CountryName = "Poland", VisitType = VisitType.Mainland };
        await _service.UpsertVisitAsync("user@example.com", visit);

        await _service.DeleteVisitAsync("user@example.com", "POL");

        var data = await _service.LoadAsync("user@example.com");
        Assert.That(data.Visits, Is.Empty);
    }

    [Test]
    public async Task SaveAsync_UpdatesLastModified()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var data = new TravelData { UserEmail = "user@example.com", Visits = new() };

        await _service.SaveAsync(data);

        Assert.That(data.LastModified, Is.GreaterThan(before));
    }

    [Test]
    public async Task UploadPhotoAsync_AddsPhotoId_ToVisit()
    {
        await _service.UpsertVisitAsync("user@example.com",
            new CountryVisit { CountryCode = "JPN", CountryName = "Japan" });
        using var stream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF }); // jpeg magic bytes

        var photoId = await _service.UploadPhotoAsync("user@example.com", "JPN", stream, "image/jpeg");

        Assert.That(photoId, Is.Not.Null.And.Not.Empty);
        var data = await _service.LoadAsync("user@example.com");
        Assert.That(data.Visits[0].PhotoIds, Contains.Item(photoId));
    }

    [Test]
    public async Task DeletePhotoAsync_RemovesPhotoId_FromVisit()
    {
        await _service.UpsertVisitAsync("user@example.com",
            new CountryVisit { CountryCode = "JPN", CountryName = "Japan" });
        using var stream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
        var photoId = await _service.UploadPhotoAsync("user@example.com", "JPN", stream, "image/jpeg");

        await _service.DeletePhotoAsync("user@example.com", "JPN", photoId);

        var data = await _service.LoadAsync("user@example.com");
        Assert.That(data.Visits[0].PhotoIds, Does.Not.Contain(photoId));
    }

    [Test]
    public async Task GetPhotoAsync_ReturnsContent_WhenPhotoExists()
    {
        await _service.UpsertVisitAsync("user@example.com",
            new CountryVisit { CountryCode = "JPN", CountryName = "Japan" });
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF };
        using var uploadStream = new MemoryStream(bytes);
        var photoId = await _service.UploadPhotoAsync("user@example.com", "JPN", uploadStream, "image/jpeg");

        var result = await _service.GetPhotoAsync("user@example.com", "JPN", photoId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Value.ContentType, Is.EqualTo("image/jpeg"));
        var readBytes = new byte[bytes.Length];
        await result.Value.Data.ReadExactlyAsync(readBytes);
        Assert.That(readBytes, Is.EqualTo(bytes));
        result.Value.Data.Dispose();
    }

    [Test]
    public async Task GetPhotoAsync_ReturnsNull_WhenPhotoNotFound()
    {
        var result = await _service.GetPhotoAsync("user@example.com", "JPN", "nonexistent.jpg");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GenerateShareTokenAsync_ReturnsToken_AndPersistsInData()
    {
        await _service.UpsertVisitAsync("user@example.com",
            new CountryVisit { CountryCode = "POL", CountryName = "Poland" });

        var token = await _service.GenerateShareTokenAsync("user@example.com");

        Assert.That(token, Is.Not.Null.And.Not.Empty);
        var data = await _service.LoadAsync("user@example.com");
        Assert.That(data.ShareToken, Is.EqualTo(token));
    }

    [Test]
    public async Task LoadByShareTokenAsync_ReturnsData_WhenTokenValid()
    {
        await _service.UpsertVisitAsync("user@example.com",
            new CountryVisit { CountryCode = "POL", CountryName = "Poland" });
        var token = await _service.GenerateShareTokenAsync("user@example.com");

        var result = await _service.LoadByShareTokenAsync(token);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.UserEmail, Is.EqualTo("user@example.com"));
    }

    [Test]
    public async Task LoadByShareTokenAsync_ReturnsNull_WhenTokenInvalid()
    {
        var result = await _service.LoadByShareTokenAsync("nonexistent-token");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task RevokeShareTokenAsync_ClearsToken()
    {
        await _service.UpsertVisitAsync("user@example.com",
            new CountryVisit { CountryCode = "POL", CountryName = "Poland" });
        var token = await _service.GenerateShareTokenAsync("user@example.com");

        await _service.RevokeShareTokenAsync("user@example.com");

        var data = await _service.LoadAsync("user@example.com");
        Assert.That(data.ShareToken, Is.Null.Or.Empty);
        var result = await _service.LoadByShareTokenAsync(token);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task UpsertVisitAsync_PreservesIsWishlist_WhenTrue()
    {
        var visit = new CountryVisit { CountryCode = "JPN", CountryName = "Japan", IsWishlist = true };

        await _service.UpsertVisitAsync("user@example.com", visit);

        var data = await _service.LoadAsync("user@example.com");
        Assert.That(data.Visits[0].IsWishlist, Is.True);
    }

    [Test]
    public async Task UpsertVisitAsync_IsWishlistDefaultsFalse_WhenNotSet()
    {
        var visit = new CountryVisit { CountryCode = "POL", CountryName = "Poland", VisitType = VisitType.Mainland };

        await _service.UpsertVisitAsync("user@example.com", visit);

        var data = await _service.LoadAsync("user@example.com");
        Assert.That(data.Visits[0].IsWishlist, Is.False);
    }
}
