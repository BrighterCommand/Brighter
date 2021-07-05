using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Greetings.Ports.Command;
using Greetings.Ports.Events;
using GreetingsSender.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using GreetingsSender.Web.Models;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace GreetingsSender.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IAmAScopedCommandProcessor _commandProcessor;
        private readonly GreetingsDataContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IAmAScopedCommandProcessor commandProcessor, GreetingsDataContext context, ILogger<HomeController> logger)
        {
            _commandProcessor = commandProcessor;
            _context = context;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        
        public async Task<IActionResult> SendMessage()
        {
            var greetingAsync = new GreetingAsyncEvent("Hello from the web");
            var greeting = new GreetingEvent("Hello from the web");

            _context.Greetings.Add(greeting);
            _context.GreetingsAsync.Add(greetingAsync);
            
            await _context.SaveChangesAsync();
            
            _commandProcessor.Post(greeting);
            await _commandProcessor.PostAsync(greetingAsync);

            return View("Index");
        }

        [HttpGet("SaveMessageToDbAsync")]
        public async Task<IActionResult> SaveMessageToDbAsync()
        {
            var greeting = new GreetingEvent("Hello Inside Process");

            var command = new PutGreetingDirectIntoDbCommand(new Guid());
            command.Greeting = greeting;

            await _commandProcessor.SendAsync(command);
            
            return View("Index");
        }
        
        public async Task<IActionResult> SaveAndRollbackMessage()
        {
            var transaction = await _context.Database.BeginTransactionAsync();
            
            // try
            // {
                var greetingAsync = new GreetingAsyncEvent("Hello from the web - 1");
                var greeting = new GreetingEvent("Hello from the web - 1");
                
                _context.Greetings.Add(greeting);
                _context.GreetingsAsync.Add(greetingAsync);
                
                await _context.SaveChangesAsync();
            
                _commandProcessor.Post(greeting);
                await _commandProcessor.PostAsync(greetingAsync);
                //throw new Exception("Something went wrong");
            // }
            // catch (Exception e)
            // {
                await transaction.RollbackAsync();
                
            // }
            
            var greetingAsync2 = new GreetingAsyncEvent("Hello from the web - 2");
            var greeting2 = new GreetingEvent("Hello from the web - 2");
            
            _context.Greetings.Add(greeting2);
            _context.GreetingsAsync.Add(greetingAsync2);
            
            _commandProcessor.DepositPost(greeting2);

            await _context.SaveChangesAsync();
            
            await _commandProcessor.DepositPostAsync(greetingAsync2); 
            
            return View("Index");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
        }
    }
}
