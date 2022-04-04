using System.Threading.Tasks;
using GreetingsPorts.Requests;
using GreetingsWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Paramore.Brighter;

namespace GreetingsWeb.Controllers
{
    [Route("[controller]")]
    public class GreetingsController : ControllerBase
    {
        private readonly IAmACommandProcessor _commandProcessor;

        public GreetingsController(IAmACommandProcessor commandProcessor)
        {
            _commandProcessor = commandProcessor;
        }

        [HttpPost("{name}/new")]
        public async Task<ActionResult> Post(string name, NewGreeting newGreeting)
        {
            await _commandProcessor.SendAsync(new AddGreeting(name, newGreeting.Greeting));
            return Ok();
        }
    }
}
