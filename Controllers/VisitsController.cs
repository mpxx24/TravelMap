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
    private readonly TravelDataService _service;

    public VisitsController(TravelDataService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize]
    public IActionResult GetVisits()
    {
        var email = GetEmail();
        if (email == null) return Unauthorized();

        var data = _service.Load(email);
        return Ok(data.Visits);
    }

    [HttpPost]
    [Authorize]
    public IActionResult SaveVisit([FromBody] CountryVisit visit)
    {
        var email = GetEmail();
        if (email == null) return Unauthorized();

        var data = _service.Load(email);
        var existing = data.Visits.FindIndex(v =>
            v.CountryCode.Equals(visit.CountryCode, StringComparison.OrdinalIgnoreCase));

        if (existing >= 0)
            data.Visits[existing] = visit;
        else
            data.Visits.Add(visit);

        _service.Save(data);
        return Ok(visit);
    }

    [HttpDelete("{countryCode}")]
    [Authorize]
    public IActionResult DeleteVisit(string countryCode)
    {
        var email = GetEmail();
        if (email == null) return Unauthorized();

        var data = _service.Load(email);
        data.Visits.RemoveAll(v =>
            v.CountryCode.Equals(countryCode, StringComparison.OrdinalIgnoreCase));

        _service.Save(data);
        return Ok();
    }

    private string? GetEmail() =>
        User.FindFirstValue(ClaimTypes.Email);
}
