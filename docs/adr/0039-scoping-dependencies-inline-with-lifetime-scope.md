---
id: 0039-scoping-dependencies-inline-with-lifetime-scope
title: "Scoping dependencies inline with lifetime scope"
status: Proposed
author:
  - "Brighter Team"
created: 2025-01-03
summary: "Decides to create a new lifetime scope per registered subscriber when invoking Publish rather than wrapping the entire CommandProcessor in a single scope, eliminating the need for a CommandProcessorProvider and ensuring handlers don't unexpectedly share scoped dependencies."
tags:
  - "di"
  - "lifetime"
  - "pipeline"
  - "publish"
---

# 39. Scoping dependencies inline with lifetime scope

Date: 2025-01-03

## Status

Proposed

## Context

As it stands dependency Lifetime Scopes are wrapped around the `Command Processor` meaning that all options performed by an instance of the Command processor will share the same scope, this becomes problematic when using the `Publish` method as this allows for multiple `Request Handlers` to be subscribed to a single Event, this will mean that all handlers share dependencies in the same scope which is unexpected behavior.

## Decision

When the Handler Factories are configured to not be a singleton Scopes will be created for each Lifetime, and a new lifetime will be given for each registered subscriber.

## Consequences

We will no longer require a `Command processor provider` as this was only created for scoping, and Handler factories will require the lifetime scope to be passed in to all methods so it can use this for managing scopes.
