# 14. DI Friendly Framework 

Date: 2024-07-12

## Status

Accepted

## Context

We use Dependency Injection to provide dependencies to classes, often in the form of an interface. 

The original design reason for this was layering: where designers wish to use [layering](https://martinfowler.com/bliki/PresentationDomainDataLayering.html), and cannot take a dependency on a concrete type in a layer above, they must define an interface in their own layer, and have the layer above them provide a concrete implementation. As the code runs types are instantiated from the `main` method, which in turn instantiate other types as the program flow reaches them. We call the point at which we need to begin injecting dependencies: the composition root.

Over time it has become common to use Dependency Injection to support other design patterns:

- [Strategy Pattern](https://en.wikipedia.org/wiki/Strategy_pattern): where designers wish to vary the algorithm used according to context.  
- [Test Doubles](https://en.wikipedia.org/wiki/Test_double): Injection of a type into a class allows its replacement for testing. Best used with dependencies that are slow or rely on a shared fixture over for viewing details.

Dependency Injection can rely on the instantiating type deciding how to create the dependencies and pass them to the type being instantiated (using `new`). As the construction of the dependencies of dependencies becomes more complex, it is typical to use the [Factory pattern](https://en.wikipedia.org/wiki/Factory_method_pattern) to simplify constructing this dependency chain.

A related pattern is [Inversion of Control](https://en.wikipedia.org/wiki/Inversion_of_control), sometimes called the [Hollywood Principle](https://martinfowler.com/bliki/HollywoodPrinciple.html) (Don't Call Us, We Will Call You). In this approach a framework is at the top-level, usually run from `main` and calls your code (as opposed to your code calling a library). As a result, the framework often needs to be able to create instances of user defined classes (that typically implement an interface defined by the framework). Because the framework cannot know how to create instances of your types, it needs to delegate that back to you. It needs a Factory it can call to create instances of your types.

An IoC container is a library that provides a Factory to a framework.

Brighter and Darker both need to create instances of user-defined handlers. As such they need to be able to call a factory that you define to create instances of your types.

The intersection of these two needs means that an IoC container is often used for DI - but this is not required.

A typical problem is that if you intend to use multiple frameworks, do they use the same Inversion of Control container? If they do not, you would be faced with having to register your user-defined class with multiple IoC containers. A typical response is for the framework to abstract an IoC container in it's own code, and write an [Adapter](https://en.wikipedia.org/wiki/Adapter_pattern) for each actual framework in usage.

But stepping back, a simpler model is for the framework to instead define the Factories that it needs to create instances of user-defined code, and allow the user to decide how to implement the factory. An IoC container is just a way to provide the Factory we need, our need is the Factory. 

This approach, depending on the Factory not the IoC container, is the one that Mark Seemann defines in [DI-Friendly Framework](https://blog.ploeh.dk/2014/05/19/di-friendly-framework/).

## Decision

We will use the Factory approach, as described in DI-Friendly, and provide a Factory abstraction for each family of user defined types that we need to construct, such as `IAmAHandlerFactory` for creating `IHandleRequest` instances or `IAmAMessageMapperFactory` for creating `IAmAMessageMapper` instances. We will use an abstraction per-family and not a generic Factory.

As such, we will not create an abstraction of an IoC container.

## Consequences

The majority of .NET Core users work with `Host` for console applications and thus use `ServiceCollection` as an IoC container. For convenience we provide implementation of our factories that are implemented via `ServiceCollection` so that users can work with their regular IoC container and we create the relevant factories as part of our extensions to `HostBuilder`.

However, we also provide a range of `SimpleX` factory implementations. These make testing easy as they do not require the usage of `ServiceCollection` within our tests, but they could be used as a replacement for `ServiceCollection`in non `Host` projects.
