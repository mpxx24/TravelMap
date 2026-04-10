using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelMap.Services;

namespace TravelMap.Controllers;

public class ShareController : Controller
{
    private readonly ITravelDataService _service;
    private readonly ILogger<ShareController> _logger;
    private static readonly JsonSerializerOptions _webJson = new(JsonSerializerDefaults.Web);

    public ShareController(ITravelDataService service, ILogger<ShareController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("/share/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> Index(string token, CancellationToken ct)
    {
        var data = await _service.LoadByShareTokenAsync(token, ct);
        if (data == null) return NotFound("Shared map not found or link has been revoked.");

        ViewBag.InitialVisitsJson = JsonSerializer.Serialize(data.Visits, _webJson);
        ViewBag.IsReadOnly = true;
        _logger.LogInformation("Shared map viewed for token {Token}", token);
        return View();
    }

    [HttpPost("/api/share")]
    [Authorize]
    public async Task<IActionResult> GenerateTokenAsync(CancellationToken ct)
    {
        var email = GetEmail();
        if (email == null) return Unauthorized();

        var token = await _service.GenerateShareTokenAsync(email, ct);
        var shareUrl = Url.Action("Index", "Share", new { token }, Request.Scheme)!;
        _logger.LogInformation("Share token generated for {Email}", email);
        return Ok(new { token, shareUrl });
    }

    [HttpDelete("/api/share")]
    [Authorize]
    public async Task<IActionResult> RevokeTokenAsync(CancellationToken ct)
    {
        var email = GetEmail();
        if (email == null) return Unauthorized();

        await _service.RevokeShareTokenAsync(email, ct);
        _logger.LogInformation("Share token revoked for {Email}", email);
        return Ok();
    }

    private string? GetEmail() => User.FindFirstValue(ClaimTypes.Email);
}
