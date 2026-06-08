# Ubiquitous Language

The domain of SlimStateMachine is *modeling state machines* — the library has no business domain of its own, so its vocabulary is the vocabulary of states and transitions. These are the terms callers see in the public API and should use in code, docs, and samples.

## Core concepts

| Term            | Definition                                                                              | Aliases to avoid                  |
| --------------- | --------------------------------------------------------------------------------------- | --------------------------------- |
| **State**       | One member of the `TEnum` the machine is defined over                                   | Status, stage, step, mode         |
| **Entity**      | An instance of `TEntity` whose State property the machine reads and writes              | Object, model, item, subject      |
| **Transition**  | A move of an Entity from one State to another                                           | Change, move, step, switch        |
| **State machine** | The configured set of States and the Transitions allowed between them, keyed by `TEntity`/`TEnum` | Workflow, flow, FSM       |

## Configuration

| Term                | Definition                                                                                | Aliases to avoid              |
| ------------------- | ----------------------------------------------------------------------------------------- | ----------------------------- |
| **Initial state**   | The State an Entity is expected to start in; required, set via `SetInitialState`          | Start, default, first state   |
| **Final state**     | A State with no outgoing Transitions; computed at build time, never declared              | End state, terminal, leaf      |
| **Allowed transition** | A Transition declared via `AllowTransition`; only declared Transitions can ever occur  | Edge, rule, mapping            |
| **Pre-condition**   | A `Func<TEntity, bool>` that must return true for a Transition to be permitted            | Guard, predicate, check        |
| **Post-action**     | A per-Transition `Action<TEntity>` run *before* the State property is updated             | Callback, hook, side effect    |
| **OnEntry action**  | A per-State `Action<TEntity>` run *after* the Entity enters that State                     | Enter hook, on-enter           |
| **OnExit action**   | A per-State `Action<TEntity>` run *before* the Entity leaves that State                    | Exit hook, on-leave            |

## Runtime operations

| Term                  | Definition                                                                                      | Aliases to avoid          |
| --------------------- | ----------------------------------------------------------------------------------------------- | ------------------------- |
| **Try transition**    | Attempt a Transition, honoring Pre-conditions; returns false if not allowed (`TryTransition`)   | Move, do, apply           |
| **Force transition**  | Perform a declared Transition while bypassing its Pre-condition (`ForceTransition`)             | Override, push, set state |
| **Can transition**    | Test whether a Transition is currently allowed without performing it (`CanTransition`)          | Check, validate           |
| **Transition context** | The immutable record of a completed Transition (Entity, FromState, ToState, Reason, Metadata, WasForced) passed to the `OnTransition` event | Event args, payload |
| **Reason**            | An optional human-readable string explaining why a Transition was made                          | Note, comment, message    |
| **Metadata**          | An optional key/value dictionary carried alongside a Transition                                 | Data, props, tags         |

## Relationships

- A **state machine** is uniquely keyed by a `TEntity`/`TEnum` pair and configured exactly once.
- A **state machine** declares zero or more **allowed transitions**; each connects exactly one source **State** to one target **State**.
- Each **allowed transition** has at most one **pre-condition** and at most one **post-action**.
- Each **State** has at most one **OnEntry action** and at most one **OnExit action**.
- A **final state** is derived, not declared: any **State** that is never the source of an **allowed transition**.
- A successful **try transition** or **force transition** produces one **transition context** delivered to the `OnTransition` event.

## Execution order of a successful transition

A single Transition runs these in a fixed order — note that **post-action** and **OnExit action** both fire *before* the State property changes:

1. **Post-action** (the Transition's own action)
2. **OnExit action** of the source State
3. State property is updated
4. **OnEntry action** of the target State
5. `OnTransition` event raised with the **transition context**

## Example dialogue

> **Dev:** "When an **Entity** does a **try transition**, does the **post-action** run after the **State** changes?"

> **Maintainer:** "No — despite the name, the **post-action** and the **OnExit action** both run *before* the **State** property is written. Only the **OnEntry action** and the `OnTransition` event fire afterward."

> **Dev:** "So if I want logic that sees the new **State**, I use **OnEntry**, not the **post-action**?"

> **Maintainer:** "Right. And if the move must skip its **pre-condition** — say an admin override — use a **force transition**. It still has to be an **allowed transition**; you can't invent edges that weren't declared."

> **Dev:** "What about a **State** with no outgoing **allowed transitions**?"

> **Maintainer:** "That's a **final state**. It's computed, not declared — you never mark one explicitly."

## Flagged ambiguities

- **State vs. Status.** The public API speaks entirely of **State** (`FromState`, `ToState`, `InitialState`, `SetInitialState`, `IsFinalState`), but Entities expose the value as a `Status` property and the sample enums are named `InvoiceStatus`/`OrderStatus`, while `Configure` takes a `statusPropertyAccessor`. **State** is the canonical domain term; "Status" is acceptable only as the conventional *property name* on a caller's Entity. Don't introduce "Status" into library API names.
- **Post-action vs. OnExit action.** Both are `Action<TEntity>` that run *before* the State changes, so they are easy to conflate. Keep them distinct: a **post-action** belongs to one **transition** (source→target specific); an **OnExit action** belongs to a **State** (runs on any departure from it). The name "post-action" is misleading about timing — see the execution-order section before relying on it.
- **Transition (the act) vs. allowed transition / TransitionDefinition (the rule).** "Transition" is overloaded: the runtime *occurrence* of a move versus the *configured rule* permitting it (modeled internally as `TransitionDefinition`). Prefer **allowed transition** when you mean the declared rule and plain **transition** for the runtime act.
- **Pre-condition function vs. pre-condition expression.** `AllowTransition` accepts both a `preCondition` (the executable `Func<TEntity, bool>`) and a `preConditionExpression` (a display string used only for Mermaid/D2 diagram labels). They are not kept in sync automatically — the string is documentation, not the source of truth.
