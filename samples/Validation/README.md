# Request Validation Examples

These examples show how to validate a request in the Brighter pipeline **before** the business handler runs.
Validation is opt-in per handler: you mark the handler's `Handle` method with `[ValidateRequest]` (or
`[ValidateRequestAsync]` for the async pipeline), and you choose a provider by registering one of the
validation packages. When a request is invalid the pipeline throws a `RequestValidationException` carrying the
individual failures, and the business handler never runs.

The attribute and the `RequestValidationException` live in the core `Paramore.Brighter` package
(`RequestValidation` namespace), alongside the other built-in pipeline attributes such as `[RequestLogging]`.
Each provider ships as a separate package so you only pull in the dependency you actually use.

## The three providers

### FluentValidation

`FluentValidationSample` uses the [FluentValidation](https://fluentvalidation.net/) library. Each validated
request has an `IValidator<TRequest>` registered in the container, and the provider resolves it to validate the
request. Reach for this when you want fluent, reusable rule sets that live apart from the request type.

```csharp
builder.Services.AddSingleton<IValidator<GreetingCommand>>(new GreetingCommandValidator());

builder.Services
    .AddBrighter()
    .AutoFromAssemblies()
    .UseFluentValidation();
```

### DataAnnotations

`DataAnnotationsSample` uses `System.ComponentModel.DataAnnotations`. The constraints are declared as
attributes (`[Required]`, `[EmailAddress]`, ...) on the request type itself, so nothing else needs
registering. Reach for this when the rules are simple and you want them to travel with the request.

```csharp
builder.Services
    .AddBrighter()
    .AutoFromAssemblies()
    .UseDataAnnotations();
```

### Specification

`SpecificationSample` uses Brighter's own [Specification pattern](../../docs/adr/0040-add-the-specification-pattern.md).
Each validated request has an `ISpecification<TRequest>` registered, built by composing rules with `And`/`Or`,
each carrying the `ValidationError` it reports when unsatisfied. Reach for this when you want to express rules
as composable domain specifications without an extra dependency.

> Brighter's `Specification<T>` records per-evaluation state, so register it with a **per-request lifetime**
> (transient or scoped) — not as a singleton — to keep concurrent requests isolated.

```csharp
builder.Services.AddTransient<ISpecification<PlaceOrder>>(_ => OrderSpecification.Create());

builder.Services
    .AddBrighter()
    .AutoFromAssemblies()
    .UseSpecification();
```

## Running

Each example is a small console app that sends one valid request (which reaches the handler) and one invalid
request (which is rejected before the handler runs, printing the collected errors):

```sh
dotnet run --project samples/Validation/FluentValidationSample
dotnet run --project samples/Validation/DataAnnotationsSample
dotnet run --project samples/Validation/SpecificationSample
```

## Async

Every provider has an asynchronous counterpart. Mark the handler with `[ValidateRequestAsync]` instead of
`[ValidateRequest]` and dispatch with `commandProcessor.SendAsync(...)`; registration is identical — the same
`UseFluentValidation()` / `UseDataAnnotations()` / `UseSpecification()` call wires up both pipelines.
