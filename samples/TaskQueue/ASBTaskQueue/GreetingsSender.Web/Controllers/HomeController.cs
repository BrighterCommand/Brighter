using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Greetings.Adaptors.Data;
using Greetings.Ports.Commands;
using Greetings.Ports.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using GreetingsSender.Web.Models;
using Paramore.Brighter;

namespace GreetingsSender.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly GreetingsDataContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IAmACommandProcessor commandProcessor, GreetingsDataContext context, ILogger<HomeController> logger)
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
        
        public async Task<IActionResult> DepositMessage()
        {
            var greetingAsync = new GreetingAsyncEvent("Deposit Hello from the web");
            var greeting = new GreetingEvent("Deposit Hello from the web");

            await _commandProcessor.DepositPostAsync(greetingAsync);

            _context.Greetings.Add(greeting);
            _context.GreetingsAsync.Add(greetingAsync);
            await _context.SaveChangesAsync();
            
            _commandProcessor.DepositPost(greeting);
            await _commandProcessor.DepositPostAsync(greetingAsync);
            
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
            
                _commandProcessor.DepositPost(greeting);
                await _commandProcessor.DepositPostAsync(greetingAsync);
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

            var msgs = new List<Id>();
            // msgs.Add(greeting.Id);
            // msgs.Add(greetingAsync.Id);
            msgs.Add(greeting2.Id);
            msgs.Add(greetingAsync2.Id);

            await _commandProcessor.ClearOutboxAsync(msgs);
            
            return View("Index");
        }

        public async Task<IActionResult> AddGreeting()
        {
            var command = new AddGreetingCommand {GreetingMessage = "Welcome to the Example.", ThrowError = false};
            var failingCommand = new AddGreetingCommand { GreetingMessage = "This should never reach the DB or outbox.", ThrowError = true };
            await _commandProcessor.PostAsync(command);
            await _commandProcessor.PostAsync(failingCommand);
            
            return View("Index");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
        }
    }
}
