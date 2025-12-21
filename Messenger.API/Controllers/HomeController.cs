using Microsoft.AspNetCore.Mvc;

namespace Messenger.API.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return Content("Messenger API Is Online");
        }
    }
}
