using GreetingsPorts.Requests;
using GreetingsPorts.Responses;
using GreetingsWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Paramore.Brighter;
using Paramore.Darker;

namespace GreetingsWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PeopleController : ControllerBase
    {
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly IQueryProcessor _queryProcessor;

        public PeopleController(IAmACommandProcessor commandProcessor, IQueryProcessor queryProcessor)
        {
            _commandProcessor = commandProcessor;
            _queryProcessor = queryProcessor;
        }

        [HttpGet("{name}")]
        public async Task<ActionResult<FindPersonResult>> Get(string name)
        {
            var foundPerson = await _queryProcessor.ExecuteAsync(new FindPersonByName(name));
            if (foundPerson is null)
            {
                return NotFound();
            }
            return Ok(foundPerson);
        }

        [HttpDelete("{name}")]
        public async Task<IActionResult> Delete(string name)
        {
            await _commandProcessor.SendAsync(new DeletePerson(name));
            return Ok();
        }

        [HttpPost("new")]
        public async Task<ActionResult<FindPersonResult>> Post(NewPerson newPerson)
        {
            await _commandProcessor.SendAsync(new AddPerson(newPerson.Name));

            var addedPerson = await _queryProcessor.ExecuteAsync(new FindPersonByName(newPerson.Name));

            if (addedPerson is null)
            {
                return NotFound();
            }

            return Ok(addedPerson);
        }
    }
}
