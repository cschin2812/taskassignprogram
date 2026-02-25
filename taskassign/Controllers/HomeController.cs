using Microsoft.AspNetCore.Mvc;

namespace taskassign.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction("Dashboard", "Task");
    }
}
