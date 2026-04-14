using Microsoft.AspNetCore.Mvc;

namespace MyWeb.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
