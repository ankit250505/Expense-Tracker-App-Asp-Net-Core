using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Expense_Tracker.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}