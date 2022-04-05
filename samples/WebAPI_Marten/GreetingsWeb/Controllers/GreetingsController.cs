using System.Threading.Tasks;
using GreetingsPorts.Requests;
using GreetingsPorts.Responses;
using GreetingsWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Paramore.Brighter;
using Paramore.Darker;

namespace GreetingsWeb.Controllers
{
    [Route("[controller]")]
    public class GreetingsController : ControllerBase
    {
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly IQueryProcessor _queryProcessor;

        public GreetingsController(IAmACommandProcessor commandProcessor, IQueryProcessor queryProcessor)
        {
            _commandProcessor = commandProcessor;
            _queryProcessor = queryProcessor;
        }

        [HttpGet("{name}")]
        public async Task<IActionResult> Get(string name)
        {
            var personGreetings = await _queryProcessor.ExecuteAsync(new FindPersonGreetings(name));

            if (personGreetings is null)
            {
                return NotFound();
            }

            return Ok(personGreetings);
        }

        [HttpPost("{name}/new")]
        public async Task<ActionResult<FindPersonGreetingsResult>> Post(string name, NewGreeting newGreeting)
        {
            await _commandProcessor.SendAsync(new AddGreeting(name, newGreeting.Greeting));

            var personGreetings = await _queryProcessor.ExecuteAsync(new FindPersonGreetings(name));

            if (personGreetings is null)
            {
                return NotFound();
            }

            return Ok(personGreetings);
        }
    }
}
