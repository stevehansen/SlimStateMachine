# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Test Commands

```bash
# Build the solution
dotnet build

# Run tests
dotnet test

# Run a specific test
dotnet test --filter "FullyQualifiedName~InvoiceStateMachineTests.Configure_SetsInitialState"

# Build and pack for NuGet
dotnet pack -c Release
```

## Project Overview

SlimStateMachine is a lightweight C# library for defining and managing state machines based on an entity class and an enum property representing its state. It targets .NET 9.0, .NET 8.0, and .NET Standard 2.0.

## Architecture

### Core Components

- **`StateMachine<TEntity, TEnum>`** (`StateMachine.cs`, `StateMachine.D2.cs`): Static generic class providing the public API. Uses double-checked locking for thread-safe one-time configuration. Split into partial classes:
  - Main file: Configuration, transitions (`CanTransition`, `TryTransition`, `TryTransitionAny`), queries (`GetPossibleTransitions`, `GetDefinedTransitions`, `IsFinalState`), Mermaid graph generation
  - D2 file: D2 diagram generation and `DiagramType` enum for format selection

- **`StateMachineConfigurationBuilder<TEntity, TEnum>`**: Fluent builder for configuring state machines via `Configure()`. Validates configuration and prevents duplicate transitions.

- **Internal Classes** (`Internal/`):
  - `StateMachineConfiguration<TEntity, TEnum>`: Holds compiled configuration including getter/setter delegates from expression trees, transitions dictionary (frozen after build), and computed final states
  - `TransitionDefinition<TEntity, TEnum>`: Represents a single transition with optional pre-condition function, pre-condition expression string (for diagrams), and post-action

### Key Design Patterns

- **Static generic access**: Each `TEntity`/`TEnum` combination gets its own static configuration cache
- **Expression tree compilation**: Status property accessor is compiled to delegates for efficient runtime access
- **Frozen collections**: Transitions and final states use `FrozenDictionary`/`FrozenSet` for immutable, optimized lookups
- **Pre/Post conditions**: Pre-conditions are `Func<TEntity, bool>` evaluated at transition time; post-actions are `Action<TEntity>` executed before state update

### Testing

Tests use MSTest with `[TestInitialize]` calling `ClearConfiguration_TestOnly()` to reset static state between tests. The `InternalsVisibleTo` attribute exposes internal members to the test project.
