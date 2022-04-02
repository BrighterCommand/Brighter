using System.Threading.Tasks;
using GreetingsPorts.Requests;
using GreetingsWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Paramore.Brighter;

namespace GreetingsWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PeopleController : ControllerBase
    {
        private readonly IAmACommandProcessor _commandProcessor;

        public PeopleController(IAmACommandProcessor commandProcessor)
        {
            _commandProcessor = commandProcessor;
        }

        [HttpPost("new")]
        public async Task<ActionResult> Post(NewPerson newPerson)
        {
            await _commandProcessor.SendAsync(new AddPerson(newPerson.Name));
            return Ok();
        }
    }
}
