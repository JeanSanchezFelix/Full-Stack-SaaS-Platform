using Microsoft.AspNetCore.Mvc;

namespace SvelteHybridMVC.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
