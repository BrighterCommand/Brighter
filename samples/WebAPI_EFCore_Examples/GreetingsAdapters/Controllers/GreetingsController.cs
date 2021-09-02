using System.Threading.Tasks;
using GreetingsAdapters.Models;
using GreetingsInteractors.Requests;
using GreetingsInteractors.Responses;
using Microsoft.AspNetCore.Mvc;
using Paramore.Brighter;
using Paramore.Darker;

namespace GreetingsAdapters.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GreetingsController : Controller
    {
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly IQueryProcessor _queryProcessor;

        public GreetingsController(IAmACommandProcessor commandProcessor, IQueryProcessor queryProcessor)
        {
            _commandProcessor = commandProcessor;
            _queryProcessor = queryProcessor;
        }

        [Route("{name}")]
        public async Task<IActionResult> Get(string name)
        {
             var personsGreetings = await _queryProcessor.ExecuteAsync(new FindGreetingsForPerson(name));
 
             if (personsGreetings == null) return new NotFoundResult();
 
             return Ok(personsGreetings);
        }
        
        [Route("{name}/new")]
        [HttpPost]
        public async Task<ActionResult<FindPersonsGreetings>> Post(string name, NewGreeting newGreeting)
        {
            await _commandProcessor.SendAsync(new AddGreeting(name, newGreeting.Greeting));

            var personsGreetings = await _queryProcessor.ExecuteAsync(new FindGreetingsForPerson(name));

            if (personsGreetings == null) return new NotFoundResult();

            return Ok(personsGreetings);
        }
    }
}
