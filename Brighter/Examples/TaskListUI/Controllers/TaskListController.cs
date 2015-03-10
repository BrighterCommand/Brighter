using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace TaskListUI.Controllers
{
    public class TaskListController : Controller
    {
        // GET: TaskList
        public async Task<string> Index()
        {
            var client = GetHttpClient();
            HttpResponseMessage response = await client.GetAsync("tasks");
            if (response.IsSuccessStatusCode)
            {
                var model = response.Content.ReadAsStringAsync().Result;
                return model;
           }

            return await Task.FromResult<string>(null);
        }

        //ToDo: change to Task<HttpResponseMessage>
        [HttpPost]
        public async Task<string> Create(string dueDate, string taskDescription, string taskName)
        {
            var client = GetHttpClient();
            var content = new FormUrlEncodedContent(new[] 
            {
                new KeyValuePair<string, string>("dueDate", dueDate),
                new KeyValuePair<string, string>("taskDescription", taskDescription),
                new KeyValuePair<string, string>("taskName", taskName)
            });
            HttpResponseMessage response = await client.PostAsync("tasks", content);
            if (response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Created)
                {
                    return response.Headers.Location.AbsoluteUri;
                }
            }

            return await Task.FromResult<string>(null);
        }
        //ToDo: change to Task<HttpResponseMessage>
        [HttpPost]
        public async Task<string> Delete(string href)
        {
            var client = GetHttpClient();
            var content = new FormUrlEncodedContent(new[] 
            {
                new KeyValuePair<string, string>("dueDate", dueDate),
                new KeyValuePair<string, string>("taskDescription", taskDescription),
                new KeyValuePair<string, string>("taskName", taskName)
            });
            HttpResponseMessage response = await client.PostAsync("tasks", content);
            if (response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Created)
                {
                    return response.Headers.Location.AbsoluteUri;
                }
            }

            return await Task.FromResult<string>(null);
        }

        private static HttpClient GetHttpClient()
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri("http://localhost:49743/");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }
    }
}
