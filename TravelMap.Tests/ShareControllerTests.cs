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
public class ShareControllerTests
{
    private Mock<ITravelDataService> _serviceMock = null!;
    private ShareController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _serviceMock = new Mock<ITravelDataService>();
        var logger = Mock.Of<ILogger<ShareController>>();
        _controller = new ShareController(_serviceMock.Object, logger);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Test]
    public async Task Index_SerializesVisitsAsCamelCase_WhenTokenIsValid()
    {
        var visit = new CountryVisit
        {
            CountryCode = "POL",
            CountryName = "Poland",
            VisitType = VisitType.Mainland,
            IsWishlist = false
        };
        _serviceMock.Setup(s => s.LoadByShareTokenAsync("validtoken", default))
            .ReturnsAsync(new TravelData { UserEmail = "user@example.com", Visits = [visit] });

        var result = await _controller.Index("validtoken", default);

        var view = result as ViewResult;
        Assert.That(view, Is.Not.Null);

        var json = view!.ViewData["InitialVisitsJson"] as string;
        Assert.That(json, Is.Not.Null);

        // camelCase: "countryCode" not "CountryCode"
        Assert.That(json, Does.Contain("\"countryCode\""));
        Assert.That(json, Does.Not.Contain("\"CountryCode\""));

        // camelCase: "visitType" not "VisitType"
        Assert.That(json, Does.Contain("\"visitType\""));
        Assert.That(json, Does.Not.Contain("\"VisitType\""));

        // camelCase: "isWishlist" not "IsWishlist"
        Assert.That(json, Does.Contain("\"isWishlist\""));
        Assert.That(json, Does.Not.Contain("\"IsWishlist\""));
    }

    [Test]
    public async Task Index_ReturnsNotFound_WhenTokenIsInvalid()
    {
        _serviceMock.Setup(s => s.LoadByShareTokenAsync("badtoken", default))
            .ReturnsAsync((TravelData?)null);

        var result = await _controller.Index("badtoken", default);

        Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
    }

    [Test]
    public async Task Index_SetsIsReadOnlyTrue_WhenTokenIsValid()
    {
        _serviceMock.Setup(s => s.LoadByShareTokenAsync("tok", default))
            .ReturnsAsync(new TravelData { UserEmail = "u@x.com", Visits = [] });

        var result = await _controller.Index("tok", default);

        var view = result as ViewResult;
        Assert.That(view, Is.Not.Null);
        Assert.That(view!.ViewData["IsReadOnly"], Is.EqualTo(true));
    }
}
