using System.Threading.Tasks;
using GreetingsApp.Requests;
using GreetingsApp.Responses;
using GreetingsWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Paramore.Brighter;
using Paramore.Darker;

namespace GreetingsWeb.Controllers;

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
    [HttpGet]
    public async Task<IActionResult> Get(string name)
    {
        FindPersonsGreetings personsGreetings = await _queryProcessor.ExecuteAsync(new FindGreetingsForPerson(name));

        if (personsGreetings == null || string.IsNullOrEmpty(personsGreetings.Name)) return new NotFoundResult();

        return Ok(personsGreetings);
    }

    [Route("{name}/new")]
    [HttpPost]
    public async Task<ActionResult<FindPersonsGreetings>> Post(string name, NewGreeting newGreeting)
    {
        FindPersonResult foundPerson = await _queryProcessor.ExecuteAsync(new FindPersonByName(name));

        if (foundPerson == null || foundPerson?.Person == null) return new NotFoundResult();
        
        await _commandProcessor.SendAsync(new AddGreeting(name, newGreeting.Greeting));

        FindPersonsGreetings personsGreetings = await _queryProcessor.ExecuteAsync(new FindGreetingsForPerson(name));

        if (personsGreetings == null) return new NotFoundResult();

        return Ok(personsGreetings);
    }
}
