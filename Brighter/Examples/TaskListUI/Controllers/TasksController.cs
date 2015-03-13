using System.Web.Mvc;

namespace TaskListUI.Controllers
{
    public class TasksController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}