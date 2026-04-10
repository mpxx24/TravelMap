using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TravelMap.Models;
using TravelMap.Services;

namespace TravelMap.Controllers;

[Route("api/visits")]
[ApiController]
public class VisitsController : ControllerBase
{
    private readonly ITravelDataService _service;
    private readonly ILogger<VisitsController> _logger;

    public VisitsController(ITravelDataService service, ILogger<VisitsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetVisitsAsync(CancellationToken ct)
    {
        var email = GetEmail();
        if (email == null) return Unauthorized();

        var data = await _service.LoadAsync(email, ct);
        _logger.LogInformation("Loaded {Count} visits for {Email}", data.Visits.Count, email);
        return Ok(data.Visits);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> SaveVisitAsync([FromBody] CountryVisit visit, CancellationToken ct)
    {
        if (visit == null || string.IsNullOrEmpty(visit.CountryCode))
            return BadRequest("Visit with a valid CountryCode is required.");

        var email = GetEmail();
        if (email == null) return Unauthorized();

        var saved = await _service.UpsertVisitAsync(email, visit, ct);
        _logger.LogInformation("Upserted visit for {CountryCode} by {Email}", visit.CountryCode, email);
        return Ok(saved);
    }

    [HttpDelete("{countryCode}")]
    [Authorize]
    public async Task<IActionResult> DeleteVisitAsync(string countryCode, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(countryCode))
            return BadRequest("Country code is required.");

        var email = GetEmail();
        if (email == null) return Unauthorized();

        await _service.DeleteVisitAsync(email, countryCode, ct);
        _logger.LogInformation("Deleted visit for {CountryCode} by {Email}", countryCode, email);
        return Ok();
    }

    private static readonly HashSet<string> AllowedPhotoTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/gif", "image/webp" };
    private const long MaxPhotoBytes = 5 * 1024 * 1024; // 5 MB

    [HttpPost("{countryCode}/photos")]
    [Authorize]
    public async Task<IActionResult> UploadPhotoAsync(string countryCode, IFormFile? photo, CancellationToken ct)
    {
        if (photo == null || photo.Length == 0)
            return BadRequest("A photo file is required.");
        if (!AllowedPhotoTypes.Contains(photo.ContentType))
            return BadRequest("Unsupported image type. Use JPEG, PNG, GIF, or WebP.");
        if (photo.Length > MaxPhotoBytes)
            return BadRequest("Photo exceeds the 5 MB limit.");

        var email = GetEmail();
        if (email == null) return Unauthorized();

        try
        {
            using var stream = photo.OpenReadStream();
            var photoId = await _service.UploadPhotoAsync(email, countryCode, stream, photo.ContentType, ct);
            _logger.LogInformation("Photo {PhotoId} uploaded for {CountryCode} by {Email}", photoId, countryCode, email);
            return Ok(new { photoId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{countryCode}/photos/{photoId}")]
    [Authorize]
    public async Task<IActionResult> DeletePhotoAsync(string countryCode, string photoId, CancellationToken ct)
    {
        var email = GetEmail();
        if (email == null) return Unauthorized();

        await _service.DeletePhotoAsync(email, countryCode, photoId, ct);
        _logger.LogInformation("Photo {PhotoId} deleted for {CountryCode} by {Email}", photoId, countryCode, email);
        return Ok();
    }

    [HttpGet("{countryCode}/photos/{photoId}")]
    [Authorize]
    public async Task<IActionResult> GetPhotoAsync(string countryCode, string photoId, CancellationToken ct)
    {
        var email = GetEmail();
        if (email == null) return Unauthorized();

        var result = await _service.GetPhotoAsync(email, countryCode, photoId, ct);
        if (result == null) return NotFound();
        return File(result.Value.Data, result.Value.ContentType);
    }

    private string? GetEmail() =>
        User.FindFirstValue(ClaimTypes.Email);
}
