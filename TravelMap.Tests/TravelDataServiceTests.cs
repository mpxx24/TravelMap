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
