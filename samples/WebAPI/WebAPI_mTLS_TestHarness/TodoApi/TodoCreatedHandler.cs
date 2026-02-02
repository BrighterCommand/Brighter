using Paramore.Brighter;

namespace TodoApi;

/// <summary>
/// Handler that processes TodoCreated events
/// </summary>
public class TodoCreatedHandler : RequestHandlerAsync<TodoCreated>
{
    private readonly ILogger<TodoCreatedHandler> _logger;

    public TodoCreatedHandler(ILogger<TodoCreatedHandler> logger)
    {
        _logger = logger;
    }

    public override async Task<TodoCreated> HandleAsync(TodoCreated command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Received TodoCreated event: {Title}, IsCompleted: {IsCompleted}, CreatedAt: {CreatedAt}",
            command.Title, command.IsCompleted, command.CreatedAt);

        // In a real application, you would save this to a database or perform other business logic
        // For this test harness, we just log it

        return await base.HandleAsync(command, cancellationToken);
    }
}
