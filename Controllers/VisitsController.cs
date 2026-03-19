using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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

    private string? GetEmail() =>
        User.FindFirstValue(ClaimTypes.Email);
}
