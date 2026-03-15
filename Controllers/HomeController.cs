using Microsoft.AspNetCore.Mvc;

namespace TravelMap.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();

    public IActionResult Error() => View();
}
