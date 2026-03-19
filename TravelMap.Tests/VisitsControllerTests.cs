using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using TravelMap.Controllers;
using TravelMap.Models;
using TravelMap.Services;

namespace TravelMap.Tests;

[TestFixture]
public class VisitsControllerTests
{
    private Mock<ITravelDataService> _serviceMock = null!;
    private VisitsController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceMock = new Mock<ITravelDataService>();
        _controller = CreateController(_serviceMock, "test@example.com");
    }

    private static VisitsController CreateController(Mock<ITravelDataService> serviceMock, string? email)
    {
        var logger = Mock.Of<ILogger<VisitsController>>();
        var controller = new VisitsController(serviceMock.Object, logger);

        var claims = new List<Claim>();
        if (email != null)
            claims.Add(new Claim(ClaimTypes.Email, email));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    [Test]
    public async Task GetVisitsAsync_ReturnsOkWithVisits_WhenAuthenticated()
    {
        var visits = new List<CountryVisit>
        {
            new() { CountryCode = "POL", CountryName = "Poland", VisitType = VisitType.Mainland }
        };
        _serviceMock.Setup(s => s.LoadAsync("test@example.com", default))
            .ReturnsAsync(new TravelData { UserEmail = "test@example.com", Visits = visits });

        var result = await _controller.GetVisitsAsync(default);

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.Value, Is.EqualTo(visits));
    }

    [Test]
    public async Task GetVisitsAsync_ReturnsUnauthorized_WhenNoEmail()
    {
        var controller = CreateController(_serviceMock, email: null);

        var result = await controller.GetVisitsAsync(default);

        Assert.That(result, Is.InstanceOf<UnauthorizedResult>());
    }

    [Test]
    public async Task SaveVisitAsync_ReturnsOkWithVisit_WhenValid()
    {
        var visit = new CountryVisit { CountryCode = "POL", CountryName = "Poland", VisitType = VisitType.Mainland };
        _serviceMock.Setup(s => s.UpsertVisitAsync("test@example.com", visit, default))
            .ReturnsAsync(visit);

        var result = await _controller.SaveVisitAsync(visit, default);

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.Value, Is.EqualTo(visit));
    }

    [Test]
    public async Task SaveVisitAsync_ReturnsBadRequest_WhenCountryCodeIsEmpty()
    {
        var visit = new CountryVisit { CountryCode = "", CountryName = "Poland" };

        var result = await _controller.SaveVisitAsync(visit, default);

        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task DeleteVisitAsync_ReturnsOk_WhenValid()
    {
        _serviceMock.Setup(s => s.DeleteVisitAsync("test@example.com", "POL", default))
            .Returns(Task.CompletedTask);

        var result = await _controller.DeleteVisitAsync("POL", default);

        Assert.That(result, Is.InstanceOf<OkResult>());
    }
}
