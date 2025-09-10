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
public class PeopleController : Controller
{
    private readonly IAmACommandProcessor _commandProcessor;
    private readonly IQueryProcessor _queryProcessor;

    public PeopleController(IAmACommandProcessor commandProcessor, IQueryProcessor queryProcessor)
    {
        _commandProcessor = commandProcessor;
        _queryProcessor = queryProcessor;
    }


    [Route("{name}")]
    [HttpGet]
    public async Task<ActionResult<FindPersonResult>> Get(string name)
    {
        FindPersonResult foundPerson = await _queryProcessor.ExecuteAsync(new FindPersonByName(name));

        if (foundPerson == null || foundPerson?.Person == null) return new NotFoundResult();

        return Ok(foundPerson);
    }

    [Route("{name}")]
    [HttpDelete]
    public async Task<IActionResult> Delete(string name)
    {
        await _commandProcessor.SendAsync(new DeletePerson(name));

        return Ok();
    }

    [Route("new")]
    [HttpPost]
    public async Task<ActionResult<FindPersonResult>> Post(NewPerson newPerson)
    {
        await _commandProcessor.SendAsync(new AddPerson(newPerson.Name));

        FindPersonResult addedPerson = await _queryProcessor.ExecuteAsync(new FindPersonByName(newPerson.Name));

        if (addedPerson == null) return new NotFoundResult();

        return Ok(addedPerson);
    }
}
