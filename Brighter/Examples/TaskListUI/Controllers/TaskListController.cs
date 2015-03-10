using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace TaskListUI.Controllers
{
    public class TaskListController : Controller
    {
        // GET: Tasks
        public async Task<string> Index()
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri("http://localhost:49743/");
            client.DefaultRequestHeaders.Accept.Add(
               new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await client.GetAsync("tasks");
            if (response.IsSuccessStatusCode)
            {
                var model = response.Content.ReadAsStringAsync().Result;

                return model;
           }

            return await Task.FromResult<string>(null);
        }
    }
}
