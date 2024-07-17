# 16. Use Configuration As Code

Date: 2024-07-17

## Status

Accepted

## Context

The release of [Ruby on Rails](https://en.wikipedia.org/wiki/Ruby_on_Rails) in 2005 had a major impact on how frameworks were designed. Rails promoted the idea of ["Convention-Over-Configuration"](https://en.wikipedia.org/wiki/Convention_over_configuration) that is instead of having to make decisions and configure the Rails framework, Ruby would use defaults, naming conventions, etc. such that provided you accepted those conventions you would not need to configure Rails. 

Rails' particular nemesis was prior frameworks dependency on configuration files (often XML) which were difficult to comprehend and difficult to author, frequently relying on XML based tooling. In addition, Ruby is a dynamic language. So it often works by convention, such as duck typing, where conventions must be established around the methods that properties have. So the principle of following conventions is part of how frameworks must behave.

There are disadvantages though. Some bugs are difficult to understand because they come from violations of the conventions such as spelling mistakes, and there is no way to check this. In addition the conventions often create constraints which it becomes hard to break. Finally, Rails relied on a lot of "magic" such as missing method handling in Ruby to achieve its effects, which is often hard to understand because it is not explicit.

The success of Rails heavily influenced .NET Frameworks, particularly through the standard bearers of the ALT.NET movement. Indeed Rails was a better abstraction for web development than Web Forms, which .NET developers had used previously, but many of its paradigms were derived from Ruby and were awkward when applied to a statically typed, compiled language.

## Decision

Brighter uses Configuration-As-Code and not Convention over Configuration.

Like Rails, we eschew the use of configuration files to configure how Brighter runs, because configuration by code gives us type safety that lets us eliminate who classes of errors, and does not require a context switch by the developer from a configuration file from code.

We also eschew convention based approaches to the framework, preferring the Python principle of `Explicit is better than implicit` because whilst it is slightly more typing, there is clarity about how Brighter will operate based on the choices that are made. 

For example, we use an interface `IHandleRequests` and abstract base type `RequestHandler<>` for our handlers, not a naming convention for the handler method. We believe strongly that in a strongly-typed language we should lean on the type system to be explicit about the role that a class has for our framework, and prevent classes of errors related to naming. Similarly we prefer the usage of generics to code generation.

We believe that optimizing for authorship is the wrong choice when compared to optimizing for ownership. Code spends most of its time being read, and being maintained. Being explicit about the role of a class via the type system makes ownership simpler through being explicit.

## Consequences

The consequence is that Brighter exposes all of its configuration via code. For this reason we add support for ASP.NET Core's Host Builder, which has a similar goal. See the ADR on [Push Button APIs For DSLs](0015-push-button-api-for-dsl.md).